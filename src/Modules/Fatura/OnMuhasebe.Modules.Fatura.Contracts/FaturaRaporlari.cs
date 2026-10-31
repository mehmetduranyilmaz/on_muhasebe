using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Fatura.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<KarlilikKirilimi>))]
public enum KarlilikKirilimi
{
    Urun,
    Musteri,
    Fatura
}

// Kârlılık için bir satış/iade satırı (TL). İadelerde tutarlar eksidir. Maliyet null ise satır stoktan izlenmez (hizmet, serbest satır).
public sealed record KarlilikKalemi(
    Guid FaturaId,
    string? FaturaNo,
    DateOnly Tarih,
    Guid? CariHesapId,
    string CariUnvan,
    Guid? StokKartiId,
    string? StokKodu,
    string Ad,
    decimal TemelMiktar,
    decimal NetSatisTl,
    decimal? MaliyetTl);

// MaliyetsizSatisTl: maliyeti stoktan izlenmeyen (hizmet/serbest) satış; kâra tam katılır, marj yorumunda dikkate alınmalıdır.
public sealed record KarlilikSatiri(
    Guid? Id,
    string Ad,
    string? Ek,
    decimal Miktar,
    decimal NetSatisTl,
    decimal MaliyetTl,
    decimal BrutKarTl,
    decimal? MarjYuzde,
    decimal MaliyetsizSatisTl);

public sealed record KarlilikRaporu(
    DateOnly Baslangic,
    DateOnly Bitis,
    KarlilikKirilimi Kirilim,
    decimal NetSatisTl,
    decimal MaliyetTl,
    decimal BrutKarTl,
    decimal? MarjYuzde,
    decimal MaliyetsizSatisTl,
    IReadOnlyList<KarlilikSatiri> Satirlar);

// KDV beyanı için bir fatura satırı (TL, fatura kuruyla). TevkifTl satırın tevkif edilen KDV'sidir.
public sealed record KdvKalemi(
    FaturaTuru Tur,
    int KdvOrani,
    decimal MatrahTl,
    decimal KdvTl,
    string? TevkifatKodu,
    int TevkifatPay,
    int TevkifatPayda,
    decimal TevkifTl);

// Muhtasar için bir belgenin stopajı (TL).
public sealed record StopajKalemi(FaturaTuru Tur, decimal Oran, decimal MatrahTl, decimal StopajTl);

public sealed record KdvOranSatiri(int Oran, decimal MatrahTl, decimal VergiTl);

// Tevkifatlı işlem satırı: BeyanEdilecekTl KDV1'de satıcının beyan ettiği kısım (KDV − tevkif); KDV2'de tevkif edilen kısımdır.
public sealed record KdvTevkifatSatiri(string? Kod, int KdvOrani, int Pay, int Payda, decimal MatrahTl, decimal KdvTl, decimal TevkifTl, decimal BeyanEdilecekTl);

public sealed record StopajSatiri(FaturaTuru Tur, decimal Oran, decimal MatrahTl, decimal StopajTl, int BelgeSayisi);

// KDV1 yapısında özet. Fark > 0 ise (önceki dönemden devreden düşülmeden) ödenecek, < 0 ise sonraki döneme devreden KDV'dir.
public sealed record KdvBeyanOzeti(
    int Yil,
    int Ay,
    IReadOnlyList<KdvOranSatiri> TeslimVeHizmetler,
    IReadOnlyList<KdvTevkifatSatiri> KismiTevkifatliSatislar,
    IReadOnlyList<KdvOranSatiri> AlistanIadeler,
    decimal HesaplananKdvTl,
    decimal IndirilecekKdvTl,
    decimal SorumluSifatiylaOdenenKdvTl,
    decimal SatistanIadeKdvTl,
    decimal IndirimlerToplamiTl,
    decimal FarkTl,
    IReadOnlyList<KdvTevkifatSatiri> Kdv2,
    decimal Kdv2ToplamTl,
    IReadOnlyList<StopajSatiri> Muhtasar,
    decimal MuhtasarToplamTl,
    int FaturaSayisi);

public static class FaturaRaporHesaplayici
{
    // Kârlılık kalemlerini kırılıma göre toplar; brüt kâra göre büyükten küçüğe sıralar.
    public static IReadOnlyList<KarlilikSatiri> Karlilik(IEnumerable<KarlilikKalemi> kalemler, KarlilikKirilimi kirilim) =>
        kalemler
            .GroupBy(k => kirilim switch
            {
                KarlilikKirilimi.Urun => (Id: k.StokKartiId, Ad: k.StokKartiId is null ? "Kartsız (serbest) satırlar" : k.Ad, Ek: k.StokKodu),
                KarlilikKirilimi.Musteri => (Id: k.CariHesapId, Ad: k.CariUnvan, Ek: (string?)null),
                _ => (Id: (Guid?)k.FaturaId, Ad: k.CariUnvan, Ek: k.FaturaNo)
            })
            .Select(g =>
            {
                var satis = g.Sum(k => k.NetSatisTl);
                var maliyet = g.Sum(k => k.MaliyetTl ?? 0m);
                return new KarlilikSatiri(g.Key.Id, g.Key.Ad, g.Key.Ek, g.Sum(k => k.TemelMiktar), satis, maliyet, satis - maliyet, Marj(satis - maliyet, satis),
                    g.Where(k => k.MaliyetTl is null).Sum(k => k.NetSatisTl));
            })
            .OrderByDescending(s => s.BrutKarTl)
            .ToList();

    // Brüt kâr marjı: brüt kâr / net satış (%), iki ondalık. Net satış sıfır ya da eksiyse anlamsızdır.
    public static decimal? Marj(decimal brutKar, decimal netSatis) =>
        netSatis > 0m ? Math.Round(brutKar / netSatis * 100m, 2, MidpointRounding.AwayFromZero) : null;

    // KDV1/KDV2/muhtasar özeti. Satış tarafı: satış hesaplanan KDV'dir (tevkifatlıda satıcı tevkif dışı kısmı beyan eder);
    // alıştan iade faturasının KDV'si hesaplanan KDV olarak beyan edilir. İndirimler: alış/SMM'de satıcıya ödenen KDV,
    // tevkifatlı alışta KDV2 ile sorumlu sıfatıyla ödenen kısım ve satıştan iade edilen malın KDV'si (KDVK 35).
    public static KdvBeyanOzeti KdvOzeti(int yil, int ay, IReadOnlyList<KdvKalemi> kalemler, IReadOnlyList<StopajKalemi> stopajlar, int faturaSayisi)
    {
        static bool AlisTarafi(FaturaTuru t) => t is FaturaTuru.Alis or FaturaTuru.SerbestMeslekMakbuzu or FaturaTuru.GiderPusulasi;

        var satislar = kalemler.Where(k => k.Tur == FaturaTuru.Satis).ToList();
        var teslim = OranlaraGore(satislar.Where(k => k.TevkifatPayda == 0));
        var kismi = Tevkifatlar(satislar.Where(k => k.TevkifatPayda > 0), satici: true);
        var alistanIade = OranlaraGore(kalemler.Where(k => k.Tur == FaturaTuru.AlistanIade).Select(k => k with { KdvTl = k.KdvTl - k.TevkifTl }));
        var hesaplanan = teslim.Sum(s => s.VergiTl) + kismi.Sum(s => s.BeyanEdilecekTl) + alistanIade.Sum(s => s.VergiTl);

        var alislar = kalemler.Where(k => AlisTarafi(k.Tur)).ToList();
        var indirilecek = alislar.Sum(k => k.KdvTl - k.TevkifTl);
        var sorumlu = alislar.Sum(k => k.TevkifTl);
        var satistanIade = kalemler.Where(k => k.Tur == FaturaTuru.SatistanIade).Sum(k => k.KdvTl - k.TevkifTl);
        var indirimler = indirilecek + sorumlu + satistanIade;

        var kdv2 = Tevkifatlar(alislar.Where(k => k.TevkifatPayda > 0), satici: false);
        var muhtasar = stopajlar.Where(s => s.StopajTl > 0)
            .GroupBy(s => (s.Tur, s.Oran))
            .OrderBy(g => g.Key.Tur).ThenBy(g => g.Key.Oran)
            .Select(g => new StopajSatiri(g.Key.Tur, g.Key.Oran, g.Sum(s => s.MatrahTl), g.Sum(s => s.StopajTl), g.Count()))
            .ToList();

        return new KdvBeyanOzeti(yil, ay, teslim, kismi, alistanIade, hesaplanan, indirilecek, sorumlu, satistanIade, indirimler,
            hesaplanan - indirimler, kdv2, kdv2.Sum(s => s.BeyanEdilecekTl), muhtasar, muhtasar.Sum(s => s.StopajTl), faturaSayisi);
    }

    private static List<KdvOranSatiri> OranlaraGore(IEnumerable<KdvKalemi> kalemler) =>
        kalemler.GroupBy(k => k.KdvOrani).OrderBy(g => g.Key)
            .Select(g => new KdvOranSatiri(g.Key, g.Sum(k => k.MatrahTl), g.Sum(k => k.KdvTl)))
            .ToList();

    private static List<KdvTevkifatSatiri> Tevkifatlar(IEnumerable<KdvKalemi> kalemler, bool satici) =>
        kalemler.GroupBy(k => (k.TevkifatKodu, k.KdvOrani, k.TevkifatPay, k.TevkifatPayda))
            .OrderBy(g => g.Key.TevkifatKodu).ThenBy(g => g.Key.KdvOrani)
            .Select(g =>
            {
                var (kdv, tevkif) = (g.Sum(k => k.KdvTl), g.Sum(k => k.TevkifTl));
                return new KdvTevkifatSatiri(g.Key.TevkifatKodu, g.Key.KdvOrani, g.Key.TevkifatPay, g.Key.TevkifatPayda, g.Sum(k => k.MatrahTl), kdv, tevkif,
                    satici ? kdv - tevkif : tevkif);
            })
            .ToList();
}
