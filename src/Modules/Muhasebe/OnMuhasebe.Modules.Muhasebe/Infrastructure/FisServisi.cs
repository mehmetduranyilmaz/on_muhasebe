using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Personel.Contracts;
using OnMuhasebe.Modules.Muhasebe.Domain;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;

namespace OnMuhasebe.Modules.Muhasebe.Infrastructure;

// Dönemin fişleri ve aktarım durumları. Kaynak modülün lisansı yoksa o modülün belgeleri gelmez.
internal sealed class FisServisi(
    MuhasebeDbContext db, HesapPlaniServisi planServisi, IFaturaMuhasebeSorgusu faturaSorgusu, IKasaBankaSorgusu kasaSorgusu, ICariSorgusu cariSorgusu,
    ILisansSorgusu lisans, IMevcutKullanici mevcutKullanici, IPersonelMuhasebeSorgusu personelSorgusu)
{
    public async Task<FisListesi> ListeleAsync(DateOnly baslangic, DateOnly bitis, Guid? isyeriId, CancellationToken ct)
    {
        var moduller = await ModullerAsync(ct);
        var kasaVarMi = moduller.Contains(ModulKodlari.KasaBanka);
        var (_, plan, ozel) = await planServisi.GetirAsync(ct);
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

        if (moduller.Contains(ModulKodlari.Personel))
        {
            fisler.AddRange(FisUretici.Personel(await personelSorgusu.KayitlarAsync(baslangic, bitis, isyeriId, ct), kodlar));
        }

        if (kasaVarMi)
        {
            fisler.AddRange(FisUretici.KasaBanka(await kasaSorgusu.MuhasebeHareketleriAsync(baslangic, bitis, isyeriId, ct), kodlar, uyarilar));

            // Çek/senet portföyü ve tahakkuklar işyerine bağlı değildir; işyeri seçiliyse fişe girmez.
            if (isyeriId is null)
            {
                fisler.AddRange(FisUretici.CekSenet(await kasaSorgusu.MuhasebeEvrakHareketleriAsync(baslangic, bitis, ct), kodlar));
                fisler.AddRange(FisUretici.Tahakkuk(await kasaSorgusu.MuhasebeTahakkuklariAsync(baslangic, bitis, ct), kodlar));
            }
            else
            {
                uyarilar.Add("İşyeri seçildiği için çek/senet portföy kayıtları ve tahakkuklar (firma geneli) fişlere dahil edilmedi.");
            }
        }

        uyarilar.AddRange(fisler.Where(f => !f.DengeliMi)
            .Select(f => $"{f.Tarih:dd.MM.yyyy} {f.BelgeNo} {f.BelgeTuru}: fiş dengesiz (borç {f.ToplamBorc:N2}, alacak {f.ToplamAlacak:N2})."));
        uyarilar.AddRange(fisler.Where(KodsuzMu).Select(f => $"{f.Tarih:dd.MM.yyyy} {f.BelgeNo}: hesap kodu boş satır var."));

        var kayitlar = await Aktarilanlar(baslangic, bitis, isyeriId, moduller, fisler).AsNoTracking().ToListAsync(ct);
        var aktarilan = kayitlar.ToDictionary(k => (k.Kaynak, k.KaynakId));
        var anahtarlar = fisler.Select(f => (f.Kaynak, f.KaynakId)).ToHashSet();
        var sirali = fisler
            .Select(f => f with { AktarimDurumu = Durum(f, aktarilan) })
            .OrderBy(f => f.Tarih).ThenBy(f => f.Kaynak, StringComparer.Ordinal).ThenBy(f => f.BelgeNo, StringComparer.Ordinal)
            .ToList();
        var kalkanlar = kayitlar.Where(k => !anahtarlar.Contains((k.Kaynak, k.KaynakId)))
            .OrderBy(k => k.Tarih)
            .Select(k => new KalkanFis(k.KaynakId, k.Kaynak, k.BelgeTuru, k.Tarih, k.BelgeNo))
            .ToList();

        return new FisListesi(baslangic, bitis, sirali, uyarilar.Distinct().ToList(), FisOzeti.Parmak(fisler), kalkanlar);
    }

    // Yeni aktarımın yerini alacağı kayıtlar: listedeki fişlerin ve dönemden kalkanların eski satırları.
    public async Task<List<AktarilanFis>> DegisecekKayitlarAsync(FisListesi liste, Guid? isyeriId, CancellationToken ct) =>
        await Aktarilanlar(liste.Baslangic, liste.Bitis, isyeriId, await ModullerAsync(ct), liste.Fisler).ToListAsync(ct);

    public static bool KodsuzMu(MuhasebeFisi fis) => fis.Satirlar.Any(s => s.HesapKodu.Length == 0);

    private async Task<IReadOnlyCollection<string>> ModullerAsync(CancellationToken ct) =>
        (await lisans.GetirAsync(mevcutKullanici.FirmaId!.Value, ct))?.Moduller ?? [];

    // Dönemin kapsamdaki kayıtları ile listedeki fişlerin kayıtları: tarihi dönem dışından gelen belgenin kaydı da bulunur.
    private IQueryable<AktarilanFis> Aktarilanlar(
        DateOnly baslangic, DateOnly bitis, Guid? isyeriId, IReadOnlyCollection<string> moduller, IReadOnlyList<MuhasebeFisi> fisler)
    {
        var idler = fisler.Select(f => f.KaynakId).ToList();
        var kaynaklar = LisansliKaynaklar(moduller);
        return db.AktarilanFisler.Where(k => idler.Contains(k.KaynakId)
            || (k.Tarih >= baslangic && k.Tarih <= bitis && kaynaklar.Contains(k.Kaynak) && (isyeriId == null || k.IsyeriId == isyeriId)));
    }

    // Lisansı kapanan modülün fişleri listelenmediği için "kalktı" sayılmamalıdır.
    private static List<string> LisansliKaynaklar(IReadOnlyCollection<string> moduller)
    {
        var kaynaklar = new List<string>();
        if (moduller.Contains(ModulKodlari.Fatura))
        {
            kaynaklar.Add(FisKaynaklari.Fatura);
        }

        if (moduller.Contains(ModulKodlari.KasaBanka))
        {
            kaynaklar.AddRange([FisKaynaklari.KasaBanka, FisKaynaklari.CekSenet, FisKaynaklari.Tahakkuk]);
        }

        return kaynaklar;
    }

    private static FisAktarimDurumu Durum(MuhasebeFisi fis, Dictionary<(string, Guid), AktarilanFis> aktarilan) =>
        !aktarilan.TryGetValue((fis.Kaynak, fis.KaynakId), out var kayit)
            ? FisAktarimDurumu.Aktarilmadi
            : kayit.Ozet == FisOzeti.Hesapla(fis) ? FisAktarimDurumu.Aktarildi : FisAktarimDurumu.Degisti;
}
