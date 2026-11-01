using System.Globalization;
using System.Text;
using OnMuhasebe.Shared.Contracts.TopluAktarim;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Excel;

namespace OnMuhasebe.Shared.Infrastructure.TopluAktarim;

// Şablondaki sütun. Eşanlamlılar başka programların dışa aktarımlarındaki başlıklardır ("Cari Adı", "Firma Ünvanı"...).
public sealed record AktarimSutunu(string Alan, string Baslik, bool Zorunlu = false, string[]? Esanlamlilar = null, int Genislik = 18);

public sealed record AktarimSatiri(int SatirNo, IReadOnlyDictionary<string, object?> Degerler, IReadOnlyDictionary<string, string> Basliklar)
{
    public object? Ham(string alan) => Degerler.GetValueOrDefault(alan);

    // Sayı hücresindeki kod ve vergi numarası "1234567890.00" olmasın diye ondalıksız yazılır.
    public string? Metin(string alan) => Ham(alan) switch
    {
        null => null,
        double sayi => ((decimal)sayi).ToString("0.##########", CultureInfo.InvariantCulture),
        DateTime tarih => tarih.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        var deger => Convert.ToString(deger, CultureInfo.InvariantCulture)?.Trim() is { Length: > 0 } metin ? metin : null
    };

    public decimal? Sayi(string alan, ICollection<string> hatalar)
    {
        switch (Ham(alan))
        {
            case null:
                return null;
            case double sayi:
                return (decimal)sayi;
            default:
                var metin = Metin(alan);
                if (metin is null)
                {
                    return null;
                }

                if (AktarimTablosu.SayiCoz(metin) is { } deger)
                {
                    return deger;
                }

                hatalar.Add($"{Basliklar[alan]}: \"{metin}\" sayı olarak okunamadı.");
                return null;
        }
    }

    public int? TamSayi(string alan, ICollection<string> hatalar)
    {
        var deger = Sayi(alan, hatalar);
        if (deger is { } d && (d != decimal.Truncate(d) || Math.Abs(d) > int.MaxValue))
        {
            hatalar.Add($"{Basliklar[alan]}: tam sayı olmalıdır.");
            return null;
        }

        return (int?)deger;
    }
}

public sealed record AktarimTablosu(IReadOnlyList<AktarimSatiri> Satirlar, IReadOnlyList<string> TaninmayanSutunlar)
{
    public const int AzamiSatir = 20_000;
    public const long AzamiDosyaBoyutu = 5 * 1024 * 1024;

    private const int BaslikAramaSatiri = 10;

    private static readonly Hata OkunamadiHatasi = Hata.Dogrulama("dosya_okunamadi", "Dosya okunamadı. Excel (.xlsx ya da .xls) dosyası yükleyin.");

    // Başlık, tanınan sütunu en çok olan satırdır: başka programların çıktısında üstte firma adı ve tarih satırları olabilir.
    public static Sonuc<AktarimTablosu> Oku(byte[] icerik, IReadOnlyList<AktarimSutunu> sutunlar)
    {
        if (!ExcelDosyasi.ExcelMi(icerik))
        {
            return OkunamadiHatasi;
        }

        List<ExcelSatiri> satirlar;
        try
        {
            satirlar = ExcelDosyasi.Oku(icerik, AzamiSatir + BaslikAramaSatiri, $"Tek dosyada en fazla {AzamiSatir.ToString("N0", CultureInfo.InvariantCulture)} satır aktarılabilir; dosyayı bölün.");
        }
        catch (ExcelSiniriAsildiException hata)
        {
            return Hata.Dogrulama("dosya_siniri", hata.Message);
        }
        catch (Exception hata) when (hata is not OperationCanceledException)
        {
            return OkunamadiHatasi;
        }

        // Yazımı farklı eşanlamlılar sadeleşince aynı olabilir ("Unvanı", "Ünvanı"): ilk sütun geçerlidir.
        var adlar = new Dictionary<string, string>();
        foreach (var s in sutunlar)
        {
            foreach (var ad in new[] { s.Baslik }.Concat(s.Esanlamlilar ?? []))
            {
                adlar.TryAdd(Sadele(ad), s.Alan);
            }
        }

        var baslik = satirlar.Take(BaslikAramaSatiri)
            .Select((s, i) => (Sira: i, Satir: s, Sayi: s.Hucreler.Count(h => HucreAlani(h, adlar) is not null)))
            .OrderByDescending(x => x.Sayi).ThenBy(x => x.Sira).FirstOrDefault();
        if (baslik.Satir is null || baslik.Sayi == 0)
        {
            return Hata.Dogrulama("sutun_bulunamadi", "Başlık satırı bulunamadı. Şablonu indirip sütun adlarını ona göre düzenleyin.");
        }

        var sutunAlanlari = new Dictionary<int, string>();
        var taninmayan = new List<string>();
        for (var i = 0; i < baslik.Satir.Hucreler.Length; i++)
        {
            var hucre = baslik.Satir.Hucreler[i];
            if (HucreAlani(hucre, adlar) is { } alan)
            {
                // Aynı alana iki sütun denk gelirse (Unvan ve Cari Adı) ilki kullanılır.
                if (!sutunAlanlari.ContainsValue(alan))
                {
                    sutunAlanlari[i] = alan;
                }
            }
            else if (Convert.ToString(hucre, CultureInfo.InvariantCulture)?.Trim() is { Length: > 0 } metin)
            {
                taninmayan.Add(metin);
            }
        }

        if (sutunlar.Where(s => s.Zorunlu && !sutunAlanlari.ContainsValue(s.Alan)).Select(s => s.Baslik).ToList() is { Count: > 0 } eksik)
        {
            return Hata.Dogrulama("sutun_bulunamadi", $"Zorunlu sütun bulunamadı: {string.Join(", ", eksik)}. Şablonu indirip sütun adlarını ona göre düzenleyin.");
        }

        var basliklar = sutunlar.ToDictionary(s => s.Alan, s => s.Baslik);
        var veri = satirlar.Skip(baslik.Sira + 1)
            .Select(s => new AktarimSatiri(s.SatirNo,
                sutunAlanlari.Where(k => k.Key < s.Hucreler.Length).ToDictionary(k => k.Value, k => s.Hucreler[k.Key]), basliklar))
            .ToList();
        return new AktarimTablosu(veri, taninmayan);
    }

    public static byte[] Sablon(string sayfaAdi, IReadOnlyList<AktarimSutunu> sutunlar) =>
        ExcelYazici.Yaz(sayfaAdi, sutunlar.Select(s => new ExcelSutunu(s.Zorunlu ? s.Baslik + "*" : s.Baslik, s.Genislik)).ToList(), []);

    public static TopluAktarimSonucu Ozetle(bool kaydedildiMi, IReadOnlyList<AktarimSatiriOzeti> satirlar, IReadOnlyList<string> taninmayan) =>
        new(kaydedildiMi, satirlar.Count,
            satirlar.Count(s => s.Durum == AktarimSatirDurumu.Eklenecek),
            satirlar.Count(s => s.Durum == AktarimSatirDurumu.Mukerrer),
            satirlar.Count(s => s.Durum == AktarimSatirDurumu.Hatali),
            taninmayan,
            satirlar.OrderByDescending(s => s.Durum == AktarimSatirDurumu.Hatali).ThenByDescending(s => s.Durum == AktarimSatirDurumu.Mukerrer)
                .ThenBy(s => s.SatirNo).Take(TopluAktarimSonucu.AzamiSatirOzeti).ToList());

    // Karşılaştırma için: Türkçe harf farkı, büyük/küçük harf, boşluk ve noktalama önemsiz ("Şti." = "STI", "Vergi No" = "VERGİ NO").
    public static string Sadele(string? metin)
    {
        var sade = new StringBuilder();
        foreach (var harf in metin ?? "")
        {
            var c = harf switch
            {
                'İ' or 'I' or 'ı' => 'i',
                'Ş' or 'ş' => 's',
                'Ğ' or 'ğ' => 'g',
                'Ü' or 'ü' => 'u',
                'Ö' or 'ö' => 'o',
                'Ç' or 'ç' => 'c',
                _ => char.ToLowerInvariant(harf)
            };
            if (char.IsLetterOrDigit(c))
            {
                sade.Append(c);
            }
        }

        return sade.ToString();
    }

    // Metin hücrelerindeki tutar: "1.234,56", "1,234.56", "1234,5", "1.500 TL". Tek nokta + üç hane Türkçe binlik sayılır (1.234 = 1234).
    public static decimal? SayiCoz(string metin)
    {
        var sade = new string(metin.Where(c => char.IsAsciiDigit(c) || c is '.' or ',' or '-').ToArray());
        if (sade.Length == 0 || sade.Any(char.IsAsciiDigit) == false || metin.Any(c => char.IsLetter(c)) && !ParaEkiMi(metin))
        {
            return null;
        }

        var nokta = sade.Count(c => c == '.');
        var virgul = sade.Count(c => c == ',');
        string duz;
        if (nokta > 0 && virgul > 0)
        {
            var ondalik = sade.LastIndexOf('.') > sade.LastIndexOf(',') ? '.' : ',';
            var binlik = ondalik == '.' ? ',' : '.';
            duz = sade.Replace(binlik.ToString(), "").Replace(ondalik, '.');
        }
        else if (virgul > 0)
        {
            duz = virgul > 1 ? sade.Replace(",", "") : sade.Replace(',', '.');
        }
        else if (nokta > 1 || (nokta == 1 && sade.Length - sade.IndexOf('.') - 1 == 3 && sade.TrimStart('-').IndexOf('.') is >= 1 and <= 3))
        {
            duz = sade.Replace(".", "");
        }
        else
        {
            duz = sade;
        }

        return decimal.TryParse(duz, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var deger) ? deger : null;
    }

    private static bool ParaEkiMi(string metin)
    {
        var harfler = new string(metin.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        return harfler is "TL" or "TRY" or "USD" or "EUR" or "GBP";
    }

    private static string? HucreAlani(object? hucre, Dictionary<string, string> adlar) =>
        hucre is string metin ? adlar.GetValueOrDefault(Sadele(metin)) : null;
}
