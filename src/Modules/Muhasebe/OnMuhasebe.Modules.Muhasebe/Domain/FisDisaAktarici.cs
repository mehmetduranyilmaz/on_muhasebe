using System.Globalization;
using System.Text;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Shared.Infrastructure.Excel;

namespace OnMuhasebe.Modules.Muhasebe.Domain;

// Yevmiye fiş listesi dosyaları: fişin her satırı bir dosya satırıdır, aynı fişin satırları aynı Fiş No'yu taşır.
internal static class FisDisaAktarici
{
    private static readonly string[] _basliklar =
        ["Fiş No", "Fiş Türü", "Tarih", "Belge No", "Hesap Kodu", "Hesap Adı", "Açıklama", "Borç", "Alacak", "Kaynak", "Durum"];

    private static readonly int[] _genislikler = [8, 10, 12, 20, 18, 32, 50, 14, 14, 12, 12];

    private sealed record Satir(int FisNo, MuhasebeFisi Fis, FisSatiri Hesap);

    private static IEnumerable<Satir> Satirlar(IEnumerable<MuhasebeFisi> fisler) =>
        fisler.SelectMany((f, i) => f.Satirlar.Select(s => new Satir(i + 1, f, s)));

    private static string KaynakAdi(string kaynak) => kaynak switch
    {
        FisKaynaklari.Fatura => "Fatura",
        FisKaynaklari.KasaBanka => "Kasa/Banka",
        FisKaynaklari.CekSenet => "Çek/Senet",
        _ => "Tahakkuk"
    };

    private static string DurumAdi(FisAktarimDurumu durum) => durum switch
    {
        FisAktarimDurumu.Aktarildi => "Aktarıldı",
        FisAktarimDurumu.Degisti => "Değişti",
        _ => "Yeni"
    };

    // Türkçe Excel'in doğrudan açtığı biçim: UTF-8 BOM, noktalı virgül, virgüllü ondalık.
    public static byte[] Csv(IEnumerable<MuhasebeFisi> fisler)
    {
        var metin = new StringBuilder();
        metin.Append(string.Join(';', _basliklar.Select(CsvHucre))).Append("\r\n");
        foreach (var s in Satirlar(fisler))
        {
            metin.Append(string.Join(';',
                s.FisNo.ToString(CultureInfo.InvariantCulture), CsvHucre(s.Fis.Tur.ToString()), s.Fis.Tarih.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                CsvHucre(s.Fis.BelgeNo), CsvHucre(s.Hesap.HesapKodu), CsvHucre(s.Hesap.HesapAdi), CsvHucre(s.Hesap.Aciklama),
                CsvTutar(s.Hesap.Borc), CsvTutar(s.Hesap.Alacak), CsvHucre(KaynakAdi(s.Fis.Kaynak)), CsvHucre(DurumAdi(s.Fis.AktarimDurumu))))
                .Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(metin.ToString())];
    }

    // Sunucu kültüründen bağımsız: Docker imajı Türkçe kültür verisi taşımayabilir.
    private static string CsvTutar(decimal tutar) => tutar.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    // Ünvan ve açıklama kullanıcı girdisidir: formül gibi başlayan metin Excel'de çalışmasın diye tek tırnakla etkisizleştirilir.
    private static string CsvHucre(string? deger)
    {
        var metin = deger ?? "";
        if (metin.Length > 0 && metin[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            metin = "'" + metin;
        }

        return metin.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{metin.Replace("\"", "\"\"")}\"" : metin;
    }

    public static byte[] Excel(IEnumerable<MuhasebeFisi> fisler)
    {
        var satirlar = Satirlar(fisler).ToList();
        return ExcelYazici.Yaz("Yevmiye", _basliklar.Select((b, i) => new ExcelSutunu(b, _genislikler[i])).ToList(),
            satirlar.Select(s => new object?[]
            {
                s.FisNo, s.Fis.Tur.ToString(), s.Fis.Tarih, s.Fis.BelgeNo, s.Hesap.HesapKodu, s.Hesap.HesapAdi, s.Hesap.Aciklama, s.Hesap.Borc, s.Hesap.Alacak,
                KaynakAdi(s.Fis.Kaynak), DurumAdi(s.Fis.AktarimDurumu)
            }),
            [null, null, null, null, null, null, "Toplam", satirlar.Sum(s => s.Hesap.Borc), satirlar.Sum(s => s.Hesap.Alacak)]);
    }
}
