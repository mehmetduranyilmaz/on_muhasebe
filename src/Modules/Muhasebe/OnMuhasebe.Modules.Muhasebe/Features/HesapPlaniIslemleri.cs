using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Muhasebe.Domain;
using OnMuhasebe.Modules.Muhasebe.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Contracts.Muhasebe;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Muhasebe.Features;

// Hesap planı eşleştirmesi: ana hesap kodları (THDP varsayılanlı) ve kart bazlı özel kodlar.
internal static class HesapPlaniIslemleri
{
    // Paket programların çoğu rakam ve ayıraç kabul eder; harf içeren kodlar için de izin verilir (bazı planlarda var).
    private const string KodDeseni = "^[0-9A-Za-z]+([.\\- ][0-9A-Za-z]+)*$";

    public static void Map(RouteGroupBuilder grup)
    {
        var plan = grup.MapGroup("").Yetki(MuhasebeYetkileri.Plan);
        plan.MapGet("/hesap-plani", GetirAsync);
        plan.MapPut("/hesap-plani", KaydetAsync).Dogrula<HesapPlaniIstegi>();
        plan.MapGet("/eslemeler/{tur}", EslemelerAsync);
        plan.MapPut("/eslemeler/{tur}/{kartId:guid}", EslemeKaydetAsync).Dogrula<KartEslemeIstegi>();
    }

    internal sealed class PlanDogrulayici : AbstractValidator<HesapPlaniIstegi>
    {
        public PlanDogrulayici()
        {
            RuleFor(x => x.Ayirac).NotEmpty().Must(a => a is "." or "-" or " ").WithMessage("Ayıraç nokta, tire ya da boşluk olmalıdır.");
            RuleFor(x => x.CariAltGrup).MaximumLength(10).Matches("^[0-9A-Za-z]*$").WithMessage("Cari alt grubu yalnız harf/rakam olabilir.");
            RuleFor(x => x.Kodlar).NotNull();
            RuleForEach(x => x.Kodlar).Must(k => Enum.IsDefined(k.Key) && System.Text.RegularExpressions.Regex.IsMatch(k.Value ?? "", KodDeseni) && k.Value!.Length <= 30)
                .WithMessage("Hesap kodu geçersiz: yalnız harf, rakam ve ayıraç (en çok 30 karakter).");
        }
    }

    internal sealed class EslemeDogrulayici : AbstractValidator<KartEslemeIstegi>
    {
        public EslemeDogrulayici() =>
            RuleFor(x => x.Kod).MaximumLength(30).Matches(KodDeseni).WithMessage("Hesap kodu geçersiz: yalnız harf, rakam ve ayıraç.")
                .When(x => !string.IsNullOrWhiteSpace(x.Kod));
    }

    private static async Task<IResult> GetirAsync(HesapPlaniServisi servis, CancellationToken ct)
    {
        var (ayar, plan, _) = await servis.GetirAsync(ct);
        return TypedResults.Ok(Cevap(ayar, plan));
    }

    // Yalnız varsayılandan farklı kodlar saklanır: varsayılana dönen kod silinir.
    private static async Task<IResult> KaydetAsync(HesapPlaniIstegi istek, MuhasebeDbContext db, HesapPlaniServisi servis, CancellationToken ct)
    {
        var ayar = await db.Ayarlar.SingleOrDefaultAsync(ct);
        if (ayar is null)
        {
            ayar = MuhasebeAyari.Varsayilan();
            db.Ayarlar.Add(ayar);
        }

        ayar.Guncelle(istek.Ayirac, istek.CariAltGrup);
        var mevcut = await db.HesapKodlari.ToListAsync(ct);
        foreach (var (rol, kod) in istek.Kodlar)
        {
            var kayit = mevcut.Find(k => k.Rol == rol);
            if (kod.Trim() == HesapRolleri.Varsayilan(rol))
            {
                if (kayit is not null)
                {
                    db.HesapKodlari.Remove(kayit);
                }
            }
            else if (kayit is null)
            {
                db.HesapKodlari.Add(HesapKodu.Olustur(rol, kod));
            }
            else
            {
                kayit.Degistir(kod);
            }
        }

        await db.SaveChangesAsync(ct);
        var (yeniAyar, plan, _) = await servis.GetirAsync(ct);
        return TypedResults.Ok(Cevap(yeniAyar, plan));
    }

    private static async Task<IResult> EslemelerAsync(
        string tur, HesapPlaniServisi servis, ICariSorgusu cariSorgusu, IKasaBankaSorgusu kasaSorgusu, ILisansSorgusu lisans,
        IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        if (!Enum.TryParse<KartTuru>(tur, ignoreCase: true, out var kartTuru))
        {
            return Hata.Dogrulama("kart_turu", "Kart türü Cari, KasaHesabi ya da Kategori olmalıdır.").Problem();
        }

        var (_, plan, ozel) = await servis.GetirAsync(ct);
        var kartlar = await KartlarAsync(kartTuru, cariSorgusu, kasaSorgusu, lisans, mevcutKullanici, ct);
        return TypedResults.Ok(kartlar.Select(k =>
        {
            var otomatik = OtomatikKod(plan, kartTuru, k);
            var ozelKod = ozel.GetValueOrDefault((kartTuru, k.Id));
            return new KartEslemesi(k.Id, k.Kod, k.Ad, k.Tur, otomatik, ozelKod, ozelKod ?? otomatik);
        }).ToList());
    }

    // Boş kod özel kodu kaldırır (otomatik koda döner).
    private static async Task<IResult> EslemeKaydetAsync(
        string tur, Guid kartId, KartEslemeIstegi istek, MuhasebeDbContext db, ICariSorgusu cariSorgusu, IKasaBankaSorgusu kasaSorgusu,
        ILisansSorgusu lisans, IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        if (!Enum.TryParse<KartTuru>(tur, ignoreCase: true, out var kartTuru))
        {
            return Hata.Dogrulama("kart_turu", "Kart türü Cari, KasaHesabi ya da Kategori olmalıdır.").Problem();
        }

        // Başka firmanın ya da olmayan kartın kimliği yazılamaz.
        if ((await KartlarAsync(kartTuru, cariSorgusu, kasaSorgusu, lisans, mevcutKullanici, ct)).All(k => k.Id != kartId))
        {
            return Hata.Bulunamadi("kart_bulunamadi", "Kart bulunamadı.").Problem();
        }

        var kayit = await db.KodEslemeleri.SingleOrDefaultAsync(k => k.KartTuru == kartTuru && k.KartId == kartId, ct);
        if (string.IsNullOrWhiteSpace(istek.Kod))
        {
            if (kayit is not null)
            {
                db.KodEslemeleri.Remove(kayit);
            }
        }
        else if (kayit is null)
        {
            db.KodEslemeleri.Add(KodEslemesi.Olustur(kartTuru, kartId, istek.Kod));
        }
        else
        {
            kayit.Degistir(istek.Kod);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // Kartlar kaynak modülden okunur; modülün lisansı yoksa liste boştur.
    private static async Task<IReadOnlyList<MuhasebeKarti>> KartlarAsync(
        KartTuru tur, ICariSorgusu cariSorgusu, IKasaBankaSorgusu kasaSorgusu, ILisansSorgusu lisans, IMevcutKullanici mevcutKullanici,
        CancellationToken ct)
    {
        var moduller = (await lisans.GetirAsync(mevcutKullanici.FirmaId!.Value, ct))?.Moduller ?? [];
        return tur switch
        {
            KartTuru.Cari when moduller.Contains(ModulKodlari.Cari) => await cariSorgusu.MuhasebeKartlariAsync(ct),
            KartTuru.KasaHesabi when moduller.Contains(ModulKodlari.KasaBanka) => await kasaSorgusu.HesapKartlariAsync(ct),
            KartTuru.Kategori when moduller.Contains(ModulKodlari.KasaBanka) => await kasaSorgusu.KategoriKartlariAsync(ct),
            _ => []
        };
    }

    internal static string OtomatikKod(HesapPlani plan, KartTuru tur, MuhasebeKarti k) => tur switch
    {
        KartTuru.Cari => plan.CariKodu(k.Kod, k.Tur == "Tedarikci"),
        KartTuru.KasaHesabi => plan.HesapKoduOtomatik(k.Kod, k.Tur),
        _ => plan.KategoriKodu(k.Tur)
    };

    private static HesapPlaniCevabi Cevap(MuhasebeAyari ayar, HesapPlani plan) =>
        new(ayar.Ayirac, ayar.CariAltGrup,
            HesapRolleri.Tumu.Select(r => new HesapPlaniSatiri(r, HesapRolleri.Ad(r), plan.Kod(r), HesapRolleri.Varsayilan(r))).ToList());
}
