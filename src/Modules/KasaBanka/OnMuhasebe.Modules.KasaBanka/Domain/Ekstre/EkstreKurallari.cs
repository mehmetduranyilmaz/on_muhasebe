using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;

// Başlık adlarından kolon eşlemesini tahmin eder ("İşlem Tarihi", "Açıklama", "Tutar", "Borç", "Alacak", "Bakiye", "Dekont No"...).
internal static class KolonTahmincisi
{
    private const int AranacakSatir = 25;

    public static KolonEslemesi? Tahmin(IReadOnlyList<string[]> tablo)
    {
        for (var satir = 0; satir < Math.Min(tablo.Count, AranacakSatir); satir++)
        {
            var basliklar = tablo[satir].Select(EkstreMetni.Normalize).ToArray();
            int? Bul(Func<string, bool> kosul) => basliklar.Select((b, i) => (b, i)).Where(x => kosul(x.b)).Select(x => (int?)x.i).FirstOrDefault();

            var tarih = Bul(b => b.Contains("ISLEM TARIH")) ?? Bul(b => b.Contains("TARIH") && !b.Contains("VALOR"));
            var tutar = Bul(b => b.Contains("TUTAR") && !b.Contains("BAKIYE"));
            var borc = Bul(b => (b.Contains("BORC") || b.Contains("CIKAN")) && !b.Contains("BAKIYE"));
            var alacak = Bul(b => (b.Contains("ALACAK") || b.Contains("GIREN")) && !b.Contains("BAKIYE"));
            if (tarih is null || (tutar is null && (borc is null || alacak is null)))
            {
                continue;
            }

            var aciklama = Bul(b => b.Contains("ACIKLAMA")) ?? Bul(b => b.Contains("DETAY") || b.Contains("ISLEM ADI") || b.Contains("ISLEM TURU")) ?? tarih.Value + 1;
            return new KolonEslemesi(
                satir, tarih.Value, aciklama, tutar, tutar is null ? alacak : null, tutar is null ? borc : null,
                Bul(b => b.Contains("BAKIYE")),
                Bul(b => b.Contains("DEKONT") || b.Contains("REFERANS") || b.Contains("FIS NO") || b.Contains("ISLEM NO") || b.Contains("BELGE")),
                TutarTersMi: false);
        }

        return null;
    }
}

internal sealed record CozumSonucu(IReadOnlyList<CozulmusSatir> Satirlar, int AtlananSatir);

internal static class SatirCozucu
{
    // Tarihi okunamayan satırlar (devreden bakiye, toplam, boş satırlar) sessizce atlanır.
    public static CozumSonucu Coz(IReadOnlyList<string[]> tablo, KolonEslemesi e)
    {
        var satirlar = new List<CozulmusSatir>();
        var atlanan = 0;
        foreach (var hucreler in tablo.Skip(e.BaslikSatiri + 1))
        {
            string? Hucre(int? kolon) => kolon is { } k && k >= 0 && k < hucreler.Length ? hucreler[k] : null;

            var tarih = EkstreMetni.Tarih(Hucre(e.TarihKolonu));
            decimal? tutar = e.TutarKolonu is not null
                ? EkstreMetni.Tutar(Hucre(e.TutarKolonu)) is { } t ? e.TutarTersMi ? -t : t : null
                : Math.Abs(EkstreMetni.Tutar(Hucre(e.AlacakKolonu)) ?? 0m) - Math.Abs(EkstreMetni.Tutar(Hucre(e.BorcKolonu)) ?? 0m);
            if (tarih is null || tutar is not { } deger || deger == 0)
            {
                atlanan++;
                continue;
            }

            var aciklama = Hucre(e.AciklamaKolonu)?.Trim() ?? "";
            var referans = Hucre(e.ReferansKolonu)?.Trim();
            satirlar.Add(new CozulmusSatir(tarih.Value, aciklama, deger, EkstreMetni.Tutar(Hucre(e.BakiyeKolonu)),
                string.IsNullOrWhiteSpace(referans) ? null : referans));
        }

        return new CozumSonucu(satirlar, atlanan);
    }
}

internal static class BakiyeKontrolu
{
    // Satırların bakiye kolonu yürüyen bakiyeyle tutarlı mı? Bankalar en yeniyi üstte verebilir; sıra tarihlerden anlaşılır.
    public static string? Kontrol(IReadOnlyList<CozulmusSatir> satirlar, decimal? acilis = null, decimal? kapanis = null)
    {
        if (acilis is { } a && kapanis is { } k)
        {
            var fark = a + satirlar.Sum(s => s.Tutar) - k;
            return Math.Abs(fark) > 0.01m
                ? string.Create(CultureInfo.GetCultureInfo("tr-TR"), $"Açılış bakiyesi + hareketler kapanış bakiyesini tutmuyor (fark {fark:N2}); dosya eksik olabilir.")
                : null;
        }

        var bakiyeli = satirlar.Where(s => s.Bakiye is not null).ToList();
        if (bakiyeli.Count < 2)
        {
            return null;
        }

        var sirali = bakiyeli[0].Tarih > bakiyeli[^1].Tarih ? Enumerable.Reverse(bakiyeli).ToList() : bakiyeli;
        var hatali = Enumerable.Range(1, sirali.Count - 1)
            .Count(i => Math.Abs(sirali[i - 1].Bakiye!.Value + sirali[i].Tutar - sirali[i].Bakiye!.Value) > 0.01m);
        return hatali > 0
            ? $"{hatali} satırda bakiye, önceki bakiye ile tutarın toplamını tutmuyor; dosyada eksik satır ya da farklı sıralama olabilir."
            : null;
    }
}

internal static class ParmakIzi
{
    // Aynı satırın tekrar aktarılmasını önler. Aynı dosyada birebir aynı iki satır varsa sıra numarasıyla ayrışır.
    public static List<string> Hesapla(Guid hesapId, IReadOnlyList<CozulmusSatir> satirlar)
    {
        var sayac = new Dictionary<string, int>();
        return satirlar.Select(s =>
        {
            var temel = string.Join('|', hesapId.ToString("N"), s.Tarih.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                s.Tutar.ToString("F2", CultureInfo.InvariantCulture), EkstreMetni.Normalize(s.Aciklama),
                s.Referans ?? s.Bakiye?.ToString("F2", CultureInfo.InvariantCulture) ?? "");
            var tekrar = sayac[temel] = sayac.GetValueOrDefault(temel) + 1;
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{temel}|{tekrar}")));
        }).ToList();
    }
}

internal sealed record MutabakatAdayi(Guid HareketId, DateOnly Tarih, decimal Giris, decimal Cikis);

internal static class Mutabakat
{
    public const int GunToleransi = 3;

    // Aynı yönde, aynı tutarlı, ±3 gün içindeki en yakın (eşitse en eski) hareket.
    public static MutabakatAdayi? Bul(CozulmusSatir satir, IEnumerable<MutabakatAdayi> adaylar) =>
        adaylar
            .Where(a => satir.Tutar > 0 ? a.Giris == satir.Tutar : a.Cikis == -satir.Tutar)
            .Where(a => Math.Abs(a.Tarih.DayNumber - satir.Tarih.DayNumber) <= GunToleransi)
            .OrderBy(a => Math.Abs(a.Tarih.DayNumber - satir.Tarih.DayNumber))
            .ThenBy(a => a.Tarih)
            .FirstOrDefault();
}

internal sealed record KuralVerisi(Guid Id, string AnahtarKelime, KuralYonu Yon, EkstreIslemTuru IslemTuru, Guid? CariHesapId, Guid? KategoriId, Guid? KarsiHesapId);

internal sealed record KendiHesabi(Guid Id, string? Iban, string? HesapNo);

internal sealed record Oneri(
    EkstreIslemTuru IslemTuru, Guid? CariHesapId, Guid? KategoriId, Guid? KarsiHesapId, string Kaynak, Guid? KuralId,
    Guid? KapatilacakBelgeId = null, string? KapatilacakBelgeNo = null, Guid? EvrakId = null, string? EvrakNo = null);

// Satır için işlem önerir. Öncelik: öğrenilmiş kural > kendi hesabına virman > açıklamadaki VKN/IBAN/unvandan cari > yöne göre varsayılan.
internal static partial class OneriMotoru
{
    private static readonly string[] _sirketEkleri = ["LTD", "STI", "SIRKETI", "A.S.", "AS", "SAN", "VE", "TIC", "TICARET", "SANAYI", "LIMITED", "ANONIM"];

    public static Oneri Oner(
        CozulmusSatir satir, HesapTuru hesapTuru, IReadOnlyList<KuralVerisi> kurallar, IReadOnlyList<CariEslestirmeAdayi> cariler,
        IReadOnlyList<KendiHesabi> kendiHesaplari)
    {
        var aciklama = EkstreMetni.Normalize(satir.Aciklama);
        var bitisik = aciklama.Replace(" ", "");
        var giris = satir.Tutar > 0;

        var kural = kurallar
            .Where(k => k.Yon == KuralYonu.Herhangi || (k.Yon == KuralYonu.Giris) == giris)
            .Where(k => aciklama.Contains(k.AnahtarKelime, StringComparison.Ordinal))
            .OrderByDescending(k => k.AnahtarKelime.Length)
            .FirstOrDefault();
        if (kural is not null)
        {
            return new Oneri(kural.IslemTuru, kural.CariHesapId, kural.KategoriId, kural.KarsiHesapId, "Kural", kural.Id);
        }

        var kendi = kendiHesaplari.FirstOrDefault(h =>
            (h.Iban is { Length: > 0 } iban && bitisik.Contains(iban, StringComparison.Ordinal))
            || (h.HesapNo is { } no && HesapNoAnahtari(no) is { Length: >= 4 } rakam && bitisik.Contains(rakam, StringComparison.Ordinal)));
        if (kendi is not null)
        {
            return new Oneri(EkstreIslemTuru.Virman, null, null, kendi.Id, "Kendi hesabı", null);
        }

        if (CariBul(aciklama, bitisik, cariler) is { } cari)
        {
            return new Oneri(giris ? EkstreIslemTuru.Tahsilat : EkstreIslemTuru.Odeme, cari.Cari.Id, null, null, cari.Kaynak, null);
        }

        var varsayilan = (hesapTuru, giris) switch
        {
            (HesapTuru.KrediKarti, true) => EkstreIslemTuru.Virman,
            (_, true) => EkstreIslemTuru.Tahsilat,
            _ => EkstreIslemTuru.Gider
        };
        return new Oneri(varsayilan, null, null, null, "Varsayılan", null);
    }

    private static (CariEslestirmeAdayi Cari, string Kaynak)? CariBul(string aciklama, string bitisik, IReadOnlyList<CariEslestirmeAdayi> cariler)
    {
        var numaralar = Numara().Matches(aciklama).Select(m => m.Value).ToHashSet();
        if (cariler.FirstOrDefault(c => c.VergiNo is { } v && numaralar.Contains(v)) is { } vknIle)
        {
            return (vknIle, "VKN/TCKN");
        }

        if (cariler.FirstOrDefault(c => c.Iban is { Length: > 0 } iban && bitisik.Contains(iban, StringComparison.Ordinal)) is { } ibanIle)
        {
            return (ibanIle, "IBAN");
        }

        return cariler
            .Select(c => (Cari: c, Anahtar: UnvanAnahtari(c.Unvan)))
            .Where(x => x.Anahtar.Length >= 4 && aciklama.Contains(x.Anahtar, StringComparison.Ordinal))
            .OrderByDescending(x => x.Anahtar.Length)
            .Select(x => ((CariEslestirmeAdayi, string)?)(x.Cari, "Unvan"))
            .FirstOrDefault();
    }

    // "Anadolu Gıda Ltd. Şti." -> "ANADOLU GIDA": şirket ekleri atılır, ilk iki anlamlı kelime alınır.
    public static string UnvanAnahtari(string unvan) =>
        string.Join(' ', EkstreMetni.Normalize(unvan).Replace(".", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(k => k.Length >= 2 && !_sirketEkleri.Contains(k))
            .Take(2));

    // Maskeli kart numarasında ("5400 **** 4821") ekstre de maskeli yazdığından yalnızca son rakam grubu aranır.
    private static string HesapNoAnahtari(string no) =>
        no.Contains('*') || no.Contains('X', StringComparison.OrdinalIgnoreCase)
            ? RakamGrubu().Matches(no).LastOrDefault()?.Value ?? ""
            : new(no.Where(char.IsAsciiDigit).ToArray());

    [GeneratedRegex(@"\d+")]
    private static partial Regex RakamGrubu();

    [GeneratedRegex(@"(?<!\d)\d{10,11}(?!\d)")]
    private static partial Regex Numara();
}
