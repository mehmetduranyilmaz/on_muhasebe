using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;
using OnMuhasebe.Modules.Fatura.Infrastructure;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Fatura.Features;

// Gelen e-Fatura XML'i ya da karekodundan taslak alış faturası. Okuma kayıt yazmaz; kullanıcı önizlemeyi onaylayınca taslak açılır.
internal static class BelgeOkumaIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var g = grup.MapGroup("/belge-okuma").Yetki(FaturaYetkileri.Belge);
        g.MapPost("/xml", XmlAsync).DisableAntiforgery();
        g.MapPost("/karekod", KarekodAsync).Dogrula<BelgeKarekodIstegi>();
        g.MapPost("/olustur", OlusturAsync).Dogrula<BelgedenFaturaIstegi>();
    }

    internal sealed class KarekodDogrulayici : AbstractValidator<BelgeKarekodIstegi>
    {
        public KarekodDogrulayici() => RuleFor(x => x.Metin).NotEmpty().MaximumLength(8000);
    }

    internal sealed class FaturaDogrulayici : AbstractValidator<BelgedenFaturaIstegi>
    {
        public FaturaDogrulayici()
        {
            RuleFor(x => x.CariHesapId).NotEmpty().WithMessage("Cari seçin.");
            RuleFor(x => x.Belge).NotNull();
            RuleFor(x => x.Belge.Numara).NotEmpty().MaximumLength(50).When(x => x.Belge is not null);
            RuleFor(x => x.Belge.Satirlar).NotEmpty().Must(s => s is null || s.Count <= 500).When(x => x.Belge is not null);
            RuleFor(x => x.Belge.Tur).Must(t => t is FaturaTuru.Alis or FaturaTuru.SatistanIade).When(x => x.Belge is not null);
        }
    }

    internal sealed record Bagimliliklar(
        FaturaDbContext Db, ICariSorgusu Cari, IStokSorgusu Stok, IFirmaRehberi Rehber, ILisansSorgusu Lisans, IMevcutKullanici Kullanici);

    private static async Task<IResult> XmlAsync(IFormFile? dosya, [AsParameters] Bagimliliklar b, CancellationToken ct)
    {
        if (dosya is null || dosya.Length == 0)
        {
            return BelgeHatalari.XmlOkunamadi.Problem();
        }

        if (dosya.Length > UblOkuyucu.AzamiBoyut)
        {
            return BelgeHatalari.BuyukDosya.Problem();
        }

        using var bellek = new MemoryStream();
        await dosya.CopyToAsync(bellek, ct);
        var icerik = bellek.ToArray();
        var okunan = UblOkuyucu.Oku(icerik);
        return okunan.BasariliMi
            ? TypedResults.Ok(await OnizlemeAsync(okunan.Deger, UblOkuyucu.SatirKodlari(icerik), b, ct))
            : okunan.Hata.Problem();
    }

    private static async Task<IResult> KarekodAsync(BelgeKarekodIstegi istek, [AsParameters] Bagimliliklar b, CancellationToken ct)
    {
        var okunan = FaturaKarekodu.Oku(istek.Metin);
        return okunan.BasariliMi ? TypedResults.Ok(await OnizlemeAsync(okunan.Deger, [], b, ct)) : okunan.Hata.Problem();
    }

    private static async Task<BelgeOnizleme> OnizlemeAsync(OkunanBelge belge, IReadOnlyList<string?> kodlar, Bagimliliklar b, CancellationToken ct)
    {
        var firmaId = b.Kullanici.FirmaId!.Value;
        var uyarilar = new List<string>(belge.Uyarilar);
        if (BelgeKontrolu.AliciUyarisi(belge.AliciVkn, await b.Rehber.VergiNoAsync(firmaId, ct)) is { } alici)
        {
            uyarilar.Add(alici);
        }

        var cari = belge.TedarikciVkn is { Length: > 0 } vkn ? (await b.Cari.EslestirmeAdaylariAsync(ct)).FirstOrDefault(c => c.VergiNo == vkn) : null;
        Guid? mevcut = null;
        if (cari is not null)
        {
            mevcut = await b.Db.TumFaturalar.AsNoTracking().Where(f => f.CariHesapId == cari.Id && f.Numara == belge.Numara && f.Durum != FaturaDurumu.Iptal).Select(f => (Guid?)f.Id).FirstOrDefaultAsync(ct);
            if (cari.ParaBirimi != belge.ParaBirimi)
            {
                uyarilar.Add($"Cari {cari.ParaBirimi}, belge {belge.ParaBirimi}: bu para biriminde bir cari seçin.");
            }
        }

        var (satirlar, eslesen) = await StokEslestirAsync(belge.Satirlar, kodlar, b, firmaId, uyarilar, ct);
        var hesap = FaturaHesaplayici.Hesapla(satirlar.Select(s => new HesapSatiri(s.Miktar, s.BirimFiyat, s.KdvDahilMi, s.KdvOrani, s.Iskonto1, s.Iskonto2, s.Iskonto3, s.TevkifatPay, s.TevkifatPayda)).ToList(), GenelIskontoTuru.Yuzde, 0);
        if (BelgeKontrolu.ToplamUyarisi(belge.Odenecek, hesap.OdenecekTutar) is { } toplam)
        {
            uyarilar.Add(toplam);
        }

        return new BelgeOnizleme(belge with { Satirlar = satirlar }, cari?.Id, cari?.Unvan, mevcut, eslesen, uyarilar);
    }

    // Satıcının ürün kodu stok kartının kodu ya da barkoduysa satır karta bağlanır: faturayla stok girer. Lot ve seri takipli kartta bilgi faturada girildiği için bağlanmaz.
    private static async Task<(IReadOnlyList<FaturaSatiriIstegi> Satirlar, int Eslesen)> StokEslestirAsync(
        IReadOnlyList<FaturaSatiriIstegi> satirlar, IReadOnlyList<string?> kodlar, Bagimliliklar b, Guid firmaId, List<string> uyarilar, CancellationToken ct)
    {
        var aranan = kodlar.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k!).Distinct().ToList();
        if (aranan.Count == 0 || !((await b.Lisans.GetirAsync(firmaId, ct))?.Moduller ?? []).Contains(ModulKodlari.Stok))
        {
            return (satirlar, 0);
        }

        var kartlar = await b.Stok.KodlaBulAsync(aranan, ct);
        var sonuc = new List<FaturaSatiriIstegi>(satirlar.Count);
        var eslesen = 0;
        for (var i = 0; i < satirlar.Count; i++)
        {
            var s = satirlar[i];
            if (i < kodlar.Count && kodlar[i] is { } kod && kartlar.TryGetValue(kod, out var kart) && kart.AktifMi && kart.BirimTanimliMi(s.Birim))
            {
                if (kart.Izleme == IzlemeTuru.Yok)
                {
                    sonuc.Add(s with { StokKartiId = kart.Id, Ad = kart.Ad });
                    eslesen++;
                    continue;
                }

                uyarilar.Add($"\"{s.Ad}\" lot/seri takipli karta denk geliyor; taslakta karttan seçip lot bilgisini girin.");
            }

            sonuc.Add(s);
        }

        return (sonuc, eslesen);
    }

    private static async Task<IResult> OlusturAsync(BelgedenFaturaIstegi istek, FaturaDbContext db, ICariSorgusu cari, FaturaKayitServisi kayit, IsyeriSecici isyeriSecici, CancellationToken ct)
    {
        var bilgi = await cari.GetirAsync(istek.CariHesapId, ct);
        if (bilgi is not null && bilgi.ParaBirimi != istek.Belge.ParaBirimi)
        {
            return BelgeHatalari.ParaBirimi.Problem();
        }

        var numara = istek.Belge.Numara.Trim().ToUpperInvariant();
        if (await db.TumFaturalar.AnyAsync(f => f.CariHesapId == istek.CariHesapId && f.Numara == numara && f.Durum != FaturaDurumu.Iptal, ct))
        {
            return FaturaHatalari.NumaraKullaniliyor.Problem();
        }

        var isyeri = await isyeriSecici.BelirleAsync(istek.IsyeriId, ct);
        if (!isyeri.BasariliMi)
        {
            return isyeri.Hata.Problem();
        }

        var b = istek.Belge;
        var aciklama = b.Ettn is { Length: > 0 } ettn ? $"ETTN: {ettn}" : null;
        var sonuc = await kayit.IsyeriyleOlusturAsync(
            new FaturaKaydetIstegi(b.Tur, null, istek.CariHesapId, null, b.Tarih, b.VadeTarihi, numara, b.Kur, GenelIskontoTuru.Yuzde, 0, aciklama, b.Satirlar, IsyeriId: isyeri.Deger),
            isyeri.Deger, false, ct);
        return sonuc.BasariliMi
            ? TypedResults.Created($"/api/fatura/faturalar/{sonuc.Deger.Fatura.Id}", new { sonuc.Deger.Fatura.Id })
            : sonuc.Hata.Problem();
    }
}
