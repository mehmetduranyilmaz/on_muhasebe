using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<KrediTuru>))]
public enum KrediTuru
{
    TaksitliTicari,
    Rotatif,
    Spot,
    Diger
}

[JsonConverter(typeof(JsonStringEnumConverter<KrediDurumu>))]
public enum KrediDurumu
{
    Acik,
    Kapandi,
    Iptal
}

// Bankanın ödeme planındaki bir taksit. Vergi: faiz üzerinden BSMV ve (varsa) KKDF.
public sealed record KrediTaksitiGirdisi(int Sira, DateOnly Vade, decimal Anapara, decimal Faiz, decimal Vergi)
{
    public decimal Toplam => Anapara + Faiz + Vergi;
}

// Ödeme planı bankanın verdiği tablodur (hesaplayıcı yalnızca ön doldurur). Anapara toplamı kullandırılan tutara eşit olmalıdır.
// KullandirimMasrafi: dosya masrafı vb.; kullandırım günü hesaptan gider olarak çıkar.
public sealed record KrediKaydetIstegi(
    string Ad,
    KrediTuru Tur,
    Guid HesapId,
    DateOnly KullandirimTarihi,
    decimal Tutar,
    decimal AylikFaizOrani,
    decimal KullandirimMasrafi,
    string? Aciklama,
    IReadOnlyList<KrediTaksitiGirdisi> Taksitler);

// Hesap boşsa kredinin hesabından ödenir; hesabın dövizi kredininkiyle aynı olmalıdır.
public sealed record TaksitOdemeIstegi(DateOnly Tarih, Guid? HesapId);

public sealed record KrediOzeti(
    Guid Id,
    string Kod,
    string Ad,
    KrediTuru Tur,
    KrediDurumu Durum,
    string HesapAdi,
    string ParaBirimi,
    DateOnly KullandirimTarihi,
    decimal Tutar,
    decimal KalanAnapara,
    int OdenenTaksit,
    int TaksitSayisi,
    DateOnly? SonrakiVade,
    decimal? SonrakiTaksit);

public sealed record KrediTaksitiDetayi(
    int Sira,
    DateOnly Vade,
    decimal Anapara,
    decimal Faiz,
    decimal Vergi,
    decimal Toplam,
    DateOnly? OdemeTarihi,
    Guid? OdemeIslemId);

public sealed record KrediDetayi(
    Guid Id,
    string Kod,
    string Ad,
    KrediTuru Tur,
    KrediDurumu Durum,
    Guid HesapId,
    string HesapAdi,
    string ParaBirimi,
    DateOnly KullandirimTarihi,
    decimal Tutar,
    decimal AylikFaizOrani,
    decimal KullandirimMasrafi,
    string? Aciklama,
    decimal KalanAnapara,
    decimal ToplamFaiz,
    decimal ToplamVergi,
    decimal OdenenToplam,
    IReadOnlyList<KrediTaksitiDetayi> Taksitler,
    uint Surum);

public static class KrediPlaniHesaplayici
{
    // Ticari TL kredide faiz üzerinden BSMV %5, KKDF %0 yaygındır; kredi türüne/dövize göre değişir, kullanıcı değiştirebilir.
    public const decimal VarsayilanVergiOrani = 5m;

    // Eşit taksitli (annüite) plan: taksit = P·r / (1 − (1 + r)^−n), r aylık faiz. Her taksitte faiz kalan anapara × r'dir;
    // vergi faiz × vergi oranıdır. Yuvarlama farkı son taksitin anaparasına yazılır; anapara toplamı tutara eşit kalır.
    public static IReadOnlyList<KrediTaksitiGirdisi> EsitTaksit(
        decimal tutar, decimal aylikFaizYuzde, int taksitSayisi, DateOnly ilkVade, decimal vergiOraniYuzde, int aralikAy = 1)
    {
        if (tutar <= 0 || taksitSayisi <= 0)
        {
            return [];
        }

        var r = aylikFaizYuzde / 100m * aralikAy;
        decimal taksit;
        if (r == 0)
        {
            taksit = tutar / taksitSayisi;
        }
        else
        {
            var carpan = 1m;
            for (var i = 0; i < taksitSayisi; i++)
            {
                carpan *= 1 + r;
            }

            taksit = tutar * r * carpan / (carpan - 1);
        }

        var sonuc = new List<KrediTaksitiGirdisi>(taksitSayisi);
        var kalan = tutar;
        for (var i = 1; i <= taksitSayisi; i++)
        {
            var faiz = Yuvarla(kalan * r);
            var anapara = i == taksitSayisi ? kalan : Math.Min(kalan, Yuvarla(taksit - faiz));
            sonuc.Add(new KrediTaksitiGirdisi(i, ilkVade.AddMonths((i - 1) * aralikAy), anapara, faiz, Yuvarla(faiz * vergiOraniYuzde / 100m)));
            kalan -= anapara;
        }

        return sonuc;
    }

    // Planın kredi ile tutarlılığı: sıra 1..n, vadeler artan ve kullandırımdan sonra, tutarlar eksi değil, anapara toplamı = kredi tutarı.
    public static string? Hata(decimal tutar, DateOnly kullandirim, IReadOnlyList<KrediTaksitiGirdisi> taksitler)
    {
        if (taksitler.Count == 0)
        {
            return "Ödeme planında en az bir taksit olmalıdır.";
        }

        if (taksitler.Any(t => t.Anapara < 0 || t.Faiz < 0 || t.Vergi < 0 || t.Toplam <= 0))
        {
            return "Taksit tutarları eksi olamaz ve her taksitte ödeme olmalıdır.";
        }

        for (var i = 0; i < taksitler.Count; i++)
        {
            if (taksitler[i].Sira != i + 1 || taksitler[i].Vade <= (i == 0 ? kullandirim : taksitler[i - 1].Vade))
            {
                return "Taksitler sıralı olmalı; vadeler kullandırım tarihinden sonra ve artan olmalıdır.";
            }
        }

        return taksitler.Sum(t => t.Anapara) != tutar ? "Taksitlerin anapara toplamı kredi tutarına eşit olmalıdır." : null;
    }

    private static decimal Yuvarla(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}
