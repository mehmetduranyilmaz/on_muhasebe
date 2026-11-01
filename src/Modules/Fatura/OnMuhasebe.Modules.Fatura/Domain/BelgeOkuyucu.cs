using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Contracts.Birimler;
using OnMuhasebe.Shared.Contracts.Vergi;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Domain;

internal static class BelgeHatalari
{
    public static readonly Hata XmlOkunamadi = Hata.Dogrulama("belge_xml", "Dosya e-Fatura XML'i olarak okunamadı.");
    public static readonly Hata KarekodOkunamadi = Hata.Dogrulama("belge_karekod", "Karekod bir e-Fatura ya da e-Arşiv karekodu değil.");
    public static readonly Hata BuyukDosya = Hata.Dogrulama("belge_buyuk", "XML dosyası en fazla 5 MB olabilir.");
    public static readonly Hata ParaBirimi = Hata.IsKurali("belge_para_birimi", "Belgenin para birimi carinin para biriminden farklı. Bu para biriminde bir cari seçin.");
    public static readonly Hata SatirYok = Hata.Dogrulama("belge_satir", "Belgede satır bulunamadı.");
}

// UBL-TR e-Fatura XML'i. Ad alanı öneklerine bağlı kalmamak için yerel adlarla okunur; DTD ve dış kaynak kapalıdır.
internal static class UblOkuyucu
{
    public const int AzamiBoyut = 5 * 1024 * 1024;

    private static readonly CultureInfo Ic = CultureInfo.InvariantCulture;

    public static Sonuc<OkunanBelge> Oku(byte[] icerik)
    {
        if (icerik.Length > AzamiBoyut)
        {
            return BelgeHatalari.BuyukDosya;
        }

        try
        {
            using var akis = new MemoryStream(icerik);
            using var okuyucu = XmlReader.Create(akis, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = AzamiBoyut * 4L });
            return Coz(XDocument.Load(okuyucu).Root);
        }
        catch (Exception e) when (e is XmlException or FormatException or OverflowException or InvalidOperationException)
        {
            return BelgeHatalari.XmlOkunamadi;
        }
    }

    private static Sonuc<OkunanBelge> Coz(XElement? kok)
    {
        if (kok is null || kok.Name.LocalName != "Invoice" || Metin(kok, "ID") is not { Length: > 0 } numara || Metin(kok, "IssueDate") is not { } tarihMetni)
        {
            return BelgeHatalari.XmlOkunamadi;
        }

        var uyarilar = new List<string>();
        var satirlar = kok.Elements().Where(e => e.Name.LocalName == "InvoiceLine").Select(s => Satir(s, uyarilar)).ToList();
        if (satirlar.Count == 0)
        {
            return BelgeHatalari.SatirYok;
        }

        var satici = Alt(kok, "AccountingSupplierParty", "Party");
        var alici = Alt(kok, "AccountingCustomerParty", "Party");
        var tur = string.Equals(Metin(kok, "InvoiceTypeCode"), "IADE", StringComparison.OrdinalIgnoreCase) ? FaturaTuru.SatistanIade : FaturaTuru.Alis;
        var kurMetni = Metin(Alt(kok, "PricingExchangeRate"), "CalculationRate");
        var vade = Metin(Alt(kok, "PaymentMeans"), "PaymentDueDate");
        return new OkunanBelge(BelgeKaynagi.Xml, tur, numara.ToUpperInvariant(), Metin(kok, "UUID"), DateOnly.ParseExact(tarihMetni, "yyyy-MM-dd", Ic),
            vade is null ? null : DateOnly.ParseExact(vade, "yyyy-MM-dd", Ic), (Metin(kok, "DocumentCurrencyCode") ?? "TRY").ToUpperInvariant(), kurMetni is null ? null : Sayi(kurMetni),
            Vkn(satici), Unvan(satici), Metin(Alt(satici, "PartyTaxScheme", "TaxScheme"), "Name"), Vkn(alici), Metin(Alt(kok, "LegalMonetaryTotal"), "PayableAmount") is { } odenecek ? Sayi(odenecek) : null,
            satirlar, uyarilar);
    }

    private static FaturaSatiriIstegi Satir(XElement s, List<string> uyarilar)
    {
        var miktarAlani = s.Elements().First(e => e.Name.LocalName == "InvoicedQuantity");
        var miktar = Sayi(miktarAlani.Value);
        var fiyat = Sayi(Metin(Alt(s, "Price"), "PriceAmount") ?? "0");
        var birimKodu = miktarAlani.Attribute("unitCode")?.Value;
        var birim = Birimler.Tumu.FirstOrDefault(b => string.Equals(b.UneceKodu, birimKodu, StringComparison.OrdinalIgnoreCase));
        var ad = Metin(Alt(s, "Item"), "Name") ?? "Kalem";
        if (birim is null)
        {
            uyarilar.Add($"\"{ad}\" satırının birimi ({birimKodu}) tanınmadı, Adet yazıldı.");
        }

        // İskonto: oran varsa oran, yoksa tutardan hesaplanır.
        var iskonto = s.Elements().Where(e => e.Name.LocalName == "AllowanceCharge" && !string.Equals(Metin(e, "ChargeIndicator"), "true", StringComparison.OrdinalIgnoreCase)).ToList();
        var oran = 0m;
        if (iskonto.Count > 0 && miktar * fiyat > 0)
        {
            var tutar = iskonto.Sum(i => Sayi(Metin(i, "Amount") ?? "0"));
            oran = Math.Round(tutar / (miktar * fiyat) * 100, 4);
        }

        var kdv = Metin(Alt(s, "TaxTotal", "TaxSubtotal"), "Percent") is { } yuzde ? (int)Math.Round(Sayi(yuzde)) : 0;
        var tevkifatKodu = Metin(Alt(s, "WithholdingTaxTotal", "TaxSubtotal", "TaxCategory", "TaxScheme"), "TaxTypeCode");
        var tevkifat = TevkifatKodlari.Bul(tevkifatKodu);
        if (tevkifatKodu is not null && tevkifat is null)
        {
            uyarilar.Add($"\"{ad}\" satırının tevkifat kodu ({tevkifatKodu}) tanınmadı; tevkifatı kontrol edin.");
        }

        return new FaturaSatiriIstegi(null, ad, null, birim?.Kod ?? Birimler.Adet, miktar, fiyat, false, kdv, oran, 0, 0, tevkifat?.Kod, tevkifat?.Pay ?? 0, tevkifat?.Payda ?? 0);
    }

    // Satıcının ürün kodları stok eşleştirmesinde kullanılır.
    public static IReadOnlyList<string?> SatirKodlari(byte[] icerik)
    {
        using var akis = new MemoryStream(icerik);
        using var okuyucu = XmlReader.Create(akis, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(okuyucu).Root!.Elements().Where(e => e.Name.LocalName == "InvoiceLine")
            .Select(s => Metin(Alt(s, "Item", "SellersItemIdentification"), "ID") ?? Metin(Alt(s, "Item", "BuyersItemIdentification"), "ID")).ToList();
    }

    private static string? Vkn(XElement? taraf) =>
        taraf?.Elements().Where(e => e.Name.LocalName == "PartyIdentification").Select(e => e.Elements().FirstOrDefault(x => x.Name.LocalName == "ID"))
            .FirstOrDefault(x => x?.Attribute("schemeID")?.Value is "VKN" or "TCKN")?.Value.Trim();

    private static string? Unvan(XElement? taraf)
    {
        if (Metin(Alt(taraf, "PartyName"), "Name") is { Length: > 0 } ad)
        {
            return ad;
        }

        var kisi = Alt(taraf, "Person");
        var tam = $"{Metin(kisi, "FirstName")} {Metin(kisi, "FamilyName")}".Trim();
        return tam.Length > 0 ? tam : null;
    }

    private static XElement? Alt(XElement? e, params string[] yol)
    {
        foreach (var ad in yol)
        {
            e = e?.Elements().FirstOrDefault(x => x.Name.LocalName == ad);
        }

        return e;
    }

    private static string? Metin(XElement? e, string ad) =>
        e?.Elements().FirstOrDefault(x => x.Name.LocalName == ad)?.Value.Trim() is { Length: > 0 } m ? m : null;

    private static decimal Sayi(string s) => decimal.Parse(s.Trim(), NumberStyles.Float, Ic);
}

// e-Fatura ve e-Arşiv PDF'indeki karekodun JSON içeriği. Satır bilgisi yoktur, KDV oranı başına matrah vardır.
internal static partial class FaturaKarekodu
{
    private const int AzamiUzunluk = 8000;

    public static Sonuc<OkunanBelge> Oku(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin) || metin.Length > AzamiUzunluk)
        {
            return BelgeHatalari.KarekodOkunamadi;
        }

        try
        {
            using var belge = JsonDocument.Parse(metin);
            if (belge.RootElement.ValueKind != JsonValueKind.Object)
            {
                return BelgeHatalari.KarekodOkunamadi;
            }

            var alanlar = belge.RootElement.EnumerateObject().ToDictionary(a => a.Name.ToLowerInvariant().Trim(), a => a.Value.ToString().Trim(), StringComparer.Ordinal);
            return Coz(alanlar);
        }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException or ArgumentException)
        {
            return BelgeHatalari.KarekodOkunamadi;
        }
    }

    private static Sonuc<OkunanBelge> Coz(Dictionary<string, string> a)
    {
        if (!a.TryGetValue("no", out var no) || no.Length == 0 || !a.TryGetValue("tarih", out var tarih) || !a.TryGetValue("vkntckn", out var vkn))
        {
            return BelgeHatalari.KarekodOkunamadi;
        }

        var uyarilar = new List<string> { "Karekodda ürün satırları yoktur: KDV oranı başına toplam satırı açıldı. Stok girişi gerekiyorsa satırları düzenleyin." };
        var satirlar = new List<FaturaSatiriIstegi>();
        foreach (var (anahtar, deger) in a.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (KdvMatrahi().Match(anahtar) is { Success: true } m && Sayi(deger) is > 0 and var matrah)
            {
                var oran = (int)Math.Round(decimal.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
                satirlar.Add(new FaturaSatiriIstegi(null, $"{no} fatura toplamı (KDV %{oran})", null, Birimler.Adet, 1, matrah, false, oran, 0, 0, 0));
            }
        }

        if (satirlar.Count == 0)
        {
            var toplam = a.TryGetValue("malhizmettoplam", out var t) ? Sayi(t) : a.TryGetValue("vergidahil", out var v) ? Sayi(v) : 0;
            if (toplam <= 0)
            {
                return BelgeHatalari.KarekodOkunamadi;
            }

            satirlar.Add(new FaturaSatiriIstegi(null, $"{no} fatura toplamı", null, Birimler.Adet, 1, toplam, false, 0, 0, 0, 0));
            uyarilar.Add("Karekodda KDV kırılımı bulunamadı: KDV oranını kontrol edin.");
        }

        if (a.Keys.Any(k => k.Contains("tevkifat", StringComparison.Ordinal)) && a.Where(k => k.Key.Contains("tevkifat", StringComparison.Ordinal)).Any(k => Sayi(k.Value) > 0))
        {
            uyarilar.Add("Belgede tevkifat var; karekoddan kod okunamaz, satırlarda elle seçin.");
        }

        var tur = a.TryGetValue("tip", out var tip) && string.Equals(tip, "IADE", StringComparison.OrdinalIgnoreCase) ? FaturaTuru.SatistanIade : FaturaTuru.Alis;
        return new OkunanBelge(BelgeKaynagi.Karekod, tur, no.ToUpperInvariant(), a.GetValueOrDefault("ettn"), TarihCoz(tarih), null,
            a.GetValueOrDefault("parabirimi") is { Length: > 0 } para ? para.ToUpperInvariant() : "TRY", null, vkn, null, null, a.GetValueOrDefault("avkntckn"),
            a.TryGetValue("odenecek", out var o) ? Sayi(o) : null, satirlar, uyarilar);
    }

    private static DateOnly TarihCoz(string s) =>
        DateOnly.ParseExact(s, ["yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy", "yyyyMMdd"], CultureInfo.InvariantCulture);

    // Bazı üreticiler virgül kullanır; binlik ayraç karekodda görülmez.
    private static decimal Sayi(string s) => string.IsNullOrWhiteSpace(s) ? 0 : decimal.Parse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^kdvmatrah\(\s*([\d.,]+)\s*\)$")]
    private static partial Regex KdvMatrahi();
}

internal static class BelgeKontrolu
{
    private const decimal Tolerans = 0.05m;

    // Taslak olarak açılacak satırların toplamı belgeninkiyle uyuşmuyorsa kullanıcı önceden görsün.
    public static string? ToplamUyarisi(decimal? belge, decimal hesaplanan) =>
        belge is { } b && Math.Abs(b - hesaplanan) > Tolerans
            ? $"Belgenin ödenecek tutarı {b:N2}, okunan satırlardan hesaplanan {hesaplanan:N2}. Fark stopaj ya da yuvarlamadan olabilir, kontrol edin."
            : null;

    public static string? AliciUyarisi(string? belgeAlicisi, string? firmaVkn) =>
        !string.IsNullOrEmpty(belgeAlicisi) && !string.IsNullOrEmpty(firmaVkn) && belgeAlicisi != firmaVkn
            ? $"Belge bu firmaya kesilmemiş görünüyor: alıcı VKN {belgeAlicisi}, firmanın VKN'si {firmaVkn}."
            : null;
}
