using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Muhasebe.Domain;
using OnMuhasebe.Modules.Muhasebe.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Modules.Muhasebe.Features;

// Dönemin yevmiye fişleri (önizleme). Kaynak modülün lisansı yoksa o modülün belgeleri gelmez.
internal static class FisIslemleri
{
    // Mali müşavir aktarımı aylık/üç aylık yapar; bir yıl üst sınırı tek istekte sınırsız veri çekilmesini önler.
    private const int EnCokGun = 366;

    public static void Map(RouteGroupBuilder grup) =>
        grup.MapGet("/fisler", ListeleAsync).Yetki(MuhasebeYetkileri.Aktarim);

    private static async Task<IResult> ListeleAsync(
        DateOnly baslangic, DateOnly bitis, Guid? isyeriId, HesapPlaniServisi servis, IFaturaMuhasebeSorgusu faturaSorgusu,
        IKasaBankaSorgusu kasaSorgusu, ICariSorgusu cariSorgusu, ILisansSorgusu lisans, IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        if (bitis < baslangic || bitis.DayNumber - baslangic.DayNumber >= EnCokGun)
        {
            return Hata.Dogrulama("donem", "Bitiş başlangıçtan önce olamaz ve dönem en çok bir yıl olabilir.").Problem();
        }

        var moduller = (await lisans.GetirAsync(mevcutKullanici.FirmaId!.Value, ct))?.Moduller ?? [];
        var kasaVarMi = moduller.Contains(ModulKodlari.KasaBanka);
        var (_, plan, ozel) = await servis.GetirAsync(ct);
        var kodlar = new KodCozucu(plan, ozel,
            moduller.Contains(ModulKodlari.Cari) ? await cariSorgusu.MuhasebeKartlariAsync(ct) : [],
            kasaVarMi ? await kasaSorgusu.HesapKartlariAsync(ct) : [],
            kasaVarMi ? await kasaSorgusu.KategoriKartlariAsync(ct) : []);

        var uyarilar = new List<string>();
        var fisler = new List<MuhasebeFisi>();
        if (moduller.Contains(ModulKodlari.Fatura))
        {
            fisler.AddRange((await faturaSorgusu.OnayliAsync(baslangic, bitis, isyeriId, ct)).Select(f => FisUretici.Fatura(f, kodlar)));
        }

        if (kasaVarMi)
        {
            fisler.AddRange(FisUretici.KasaBanka(await kasaSorgusu.MuhasebeHareketleriAsync(baslangic, bitis, isyeriId, ct), kodlar, uyarilar));
        }

        uyarilar.AddRange(fisler.Where(f => !f.DengeliMi)
            .Select(f => $"{f.Tarih:dd.MM.yyyy} {f.BelgeNo} {f.BelgeTuru}: fiş dengesiz (borç {f.ToplamBorc:N2}, alacak {f.ToplamAlacak:N2})."));
        uyarilar.AddRange(fisler.Where(f => f.Satirlar.Any(s => s.HesapKodu == "")).Select(f => $"{f.Tarih:dd.MM.yyyy} {f.BelgeNo}: hesap kodu boş satır var."));

        return TypedResults.Ok(new FisListesi(baslangic, bitis, fisler.OrderBy(f => f.Tarih).ThenBy(f => f.Kaynak).ThenBy(f => f.BelgeNo).ToList(),
            uyarilar.Distinct().ToList()));
    }
}
