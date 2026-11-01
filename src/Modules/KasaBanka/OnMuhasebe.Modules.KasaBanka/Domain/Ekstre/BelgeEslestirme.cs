using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;

// GirisIcin: bu belgeyi hesaba giren para mı kapatır (satış faturası) yoksa çıkan para mı (alış faturası).
internal sealed record FaturaAdayi(Guid BelgeId, string BelgeNo, Guid CariId, bool GirisIcin, decimal Kalan);

internal sealed record EvrakAdayi(Guid Id, string No, Guid CariId, bool GirisIcin, decimal Tutar, DateOnly Vade);

// Ekstre önerisini açık fatura ve çek/senetle zenginleştirir; bulunan yalnız öneridir, kullanıcı aktarınca yazılır.
// Birden çok aday varsa seçim yapılmaz: yanlış belgeyi kapatmaktansa kullanıcıya bırakılır.
internal sealed class BelgeEslestirici(IEnumerable<FaturaAdayi> faturalar, IEnumerable<EvrakAdayi> evraklar)
{
    public const string FaturaNo = "Fatura no";
    public const string FaturaTutari = "Fatura tutarı";
    public const string YalnizTutar = "Yalnız tutar tuttu";
    public const string EvrakNo = "Çek/senet no";
    public const string EvrakTutari = "Çek/senet tutarı ve vadesi";

    private const int EnAzFaturaNoUzunlugu = 6;
    private const int EnAzEvrakNoUzunlugu = 3;

    private readonly List<FaturaAdayi> _faturalar = faturalar.ToList();
    private readonly List<EvrakAdayi> _evraklar = evraklar.ToList();

    public Oneri Uygula(CozulmusSatir satir, Oneri oneri, HesapTuru hesapTuru)
    {
        var giris = satir.Tutar > 0;
        var tutar = Math.Abs(satir.Tutar);
        var metin = EkstreMetni.Normalize(satir.Aciklama);
        var bitisik = metin.Replace(" ", "");
        var kelimeler = metin.Split([' ', '/', '-', ':', '.', ','], StringSplitOptions.RemoveEmptyEntries).Select(k => k.TrimStart('0')).ToHashSet();

        // Numara en güçlü kanıttır; çek numarası kısa olabildiğinden tutar da tutmalı. Fatura numarasıyla kısmi ödeme de olur.
        var numaraylaEvrak = _evraklar
            .Where(e => e.GirisIcin == giris && e.Tutar == tutar && e.No.Length >= EnAzEvrakNoUzunlugu && kelimeler.Contains(Anahtar(e.No).TrimStart('0')))
            .ToList();
        if (numaraylaEvrak.Count == 1)
        {
            return EvrakOnerisi(numaraylaEvrak[0], EvrakNo);
        }

        var numarayla = _faturalar.Where(f => f.GirisIcin == giris && Anahtar(f.BelgeNo).Length >= EnAzFaturaNoUzunlugu && bitisik.Contains(Anahtar(f.BelgeNo), StringComparison.Ordinal)).ToList();
        if (numarayla.Count == 1)
        {
            return FaturaOnerisi(numarayla[0], giris, FaturaNo);
        }

        // Öğrenilmiş kural ya da kendi hesabı, yalnız tutara dayalı tahminden güçlüdür.
        var cariOnerisi = oneri.IslemTuru is EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme && oneri.CariHesapId is not null;
        var bilgisiz = oneri.Kaynak == "Varsayılan";
        if (!cariOnerisi && !bilgisiz)
        {
            return oneri;
        }

        var vadeyle = _evraklar
            .Where(e => e.GirisIcin == giris && e.Tutar == tutar && Math.Abs(e.Vade.DayNumber - satir.Tarih.DayNumber) <= Mutabakat.GunToleransi && (bilgisiz || e.CariId == oneri.CariHesapId))
            .ToList();
        if (vadeyle.Count == 1)
        {
            return EvrakOnerisi(vadeyle[0], EvrakTutari);
        }

        if (cariOnerisi)
        {
            var carininki = _faturalar.Where(f => f.GirisIcin == giris && f.Kalan == tutar && f.CariId == oneri.CariHesapId).ToList();
            if (carininki.Count != 1)
            {
                return oneri;
            }

            _faturalar.Remove(carininki[0]);
            return oneri with { KapatilacakBelgeId = carininki[0].BelgeId, KapatilacakBelgeNo = carininki[0].BelgeNo, Kaynak = FaturaTutari };
        }

        // Kart ve POS hareketleri fatura ödemesi değildir; yalnız banka hesabında aranır.
        if (hesapTuru == HesapTuru.Banka)
        {
            var tutarla = _faturalar.Where(f => f.GirisIcin == giris && f.Kalan == tutar).ToList();
            if (tutarla.Count == 1)
            {
                _faturalar.Remove(tutarla[0]);
                return FaturaOnerisi(tutarla[0], giris, YalnizTutar);
            }
        }

        return oneri;
    }

    private Oneri EvrakOnerisi(EvrakAdayi e, string kaynak)
    {
        _evraklar.Remove(e);
        return new Oneri(EkstreIslemTuru.Evrak, null, null, null, kaynak, null, EvrakId: e.Id, EvrakNo: e.No);
    }

    private static Oneri FaturaOnerisi(FaturaAdayi f, bool giris, string kaynak) =>
        new(giris ? EkstreIslemTuru.Tahsilat : EkstreIslemTuru.Odeme, f.CariId, null, null, kaynak, null, KapatilacakBelgeId: f.BelgeId, KapatilacakBelgeNo: f.BelgeNo);

    private static string Anahtar(string no) => EkstreMetni.Normalize(no).Replace(" ", "");
}

internal static class EkstreDosyaKontrolu
{
    // Dosyadaki hesap, seçilen hesabın IBAN ya da hesap numarasıyla uyuşmuyorsa uyarı. Hesapta numara yoksa karşılaştırılamaz.
    public static string? HesapUyarisi(IReadOnlyCollection<string> dosyaHesaplari, string? iban, string? hesapNo)
    {
        var kayitli = new[] { iban, hesapNo }.Select(Temizle).Where(n => n.Length > 0 && !n.Contains('*') && !n.Contains('X')).ToList();
        var dosya = dosyaHesaplari.Select(Temizle).Where(n => n.Length > 0).Distinct().ToList();
        if (kayitli.Count == 0 || dosya.Count == 0)
        {
            return null;
        }

        return dosya.All(d => kayitli.Any(k => k.Contains(d, StringComparison.Ordinal) || d.Contains(k, StringComparison.Ordinal)))
            ? null
            : $"Dosyadaki hesap ({string.Join(", ", dosya)}) seçilen hesapla uyuşmuyor; yanlış hesaba yükleme yapıyor olabilirsiniz.";
    }

    public static bool ParaBirimiUyarMi(string? dosya, string hesap) =>
        dosya is not null && !string.Equals(dosya == "TL" ? "TRY" : dosya, hesap, StringComparison.OrdinalIgnoreCase);

    private static string Temizle(string? no) => new((no ?? "").ToUpperInvariant().Where(c => char.IsAsciiLetterOrDigit(c) || c == '*').ToArray());
}

internal static class MutabakatHesabi
{
    // Ekstre - program farkının listelerle açıklanamayan kısmı; dönem öncesinden gelen fark da buraya düşer.
    public static decimal Aciklanamayan(decimal ekstreBakiyesi, decimal programBakiyesi, decimal yalnizEkstrede, decimal yalnizProgramda) =>
        ekstreBakiyesi - programBakiyesi - yalnizEkstrede + yalnizProgramda;

    // Bankalar en yeniyi üstte verebilir; kapanış, tarihçe sonundaki satırın bakiyesidir.
    public static decimal? Kapanis(IReadOnlyList<CozulmusSatir> satirlar)
    {
        var bakiyeli = satirlar.Where(s => s.Bakiye is not null).ToList();
        return bakiyeli.Count == 0 ? null
            : bakiyeli[0].Tarih > bakiyeli[^1].Tarih ? bakiyeli[0].Bakiye
            : bakiyeli[^1].Bakiye;
    }
}
