namespace OnMuhasebe.Shared.Contracts.Vergi;

// GİB KDV tevkifatı kodu ve oranı (pay/payda, ör. 7/10). Kaynak: GİB UBL-TR Kod Listeleri V1.43 (Temmuz 2026), birebir.
// 6xx kısmi tevkifat; 8xx aynı işlemlerin tam tevkifatı (10/10; KDVGUT gereği tamamı alıcıca beyan edilen işlemler, ör. belirlenmiş alıcılara
// ve kamuya yapılan işlemler). Faturada satır bazında pay/payda saklanır; kullanıcı gerektiğinde oranı değiştirebilir.
// Liste GİB duyurularıyla güncel tutulmalıdır.
public sealed record TevkifatKodu(string Kod, string Aciklama, int Pay, int Payda);

public static class TevkifatKodlari
{
    public static IReadOnlyList<TevkifatKodu> Tumu { get; } =
    [
        new("601", "Yapım İşleri İle Bu İşlerle Birlikte İfa Edilen Mühendislik-Mimarlık Ve Etüt-Proje Hizmetleri", 4, 10),
        new("602", "Etüt, Plan-Proje, Danışmanlık, Denetim Ve Benzeri Hizmetler", 9, 10),
        new("603", "Makine, Teçhizat, Demirbaş Ve Taşıtlara Ait Tadil, Bakım Ve Onarım Hizmetleri", 7, 10),
        new("604", "Yemek Servis Hizmeti", 5, 10),
        new("605", "Organizasyon Hizmeti", 5, 10),
        new("606", "İşgücü Temin Hizmetleri", 9, 10),
        new("607", "Özel Güvenlik Hizmeti", 9, 10),
        new("608", "Yapı Denetim Hizmetleri", 9, 10),
        new("609", "Fason Olarak Yaptırılan Tekstil Ve Konfeksiyon İşleri, Çanta Ve Ayakkabı Dikim İşleri Ve Bu İşlere Aracılık Hizmetleri", 7, 10),
        new("610", "Turistik Mağazalara Verilen Müşteri Bulma / Götürme Hizmetleri", 9, 10),
        new("611", "Spor Kulüplerinin Yayın, Reklâm Ve İsim Hakkı Gelirlerine Konu İşlemleri", 9, 10),
        new("612", "Temizlik Hizmeti", 9, 10),
        new("613", "Çevre Ve Bahçe Bakım Hizmetleri", 9, 10),
        new("614", "Servis Taşımacılığı Hizmeti", 5, 10),
        new("615", "Her Türlü Baskı Ve Basım Hizmetleri", 7, 10),
        new("616", "Diğer Hizmetler", 5, 10),
        new("617", "Hurda Metalden Elde Edilen Külçe Teslimleri", 7, 10),
        new("618", "Hurda Metalden Elde Edilenler Dışındaki Bakır, Çinko Demir, Çelik Alüminyum Ve Kurşun Külçe Teslimleri", 7, 10),
        new("619", "Bakır, Çinko Ve Alüminyum Ürünlerinin Teslimi", 7, 10),
        new("620", "İstisnadan Vazgeçenlerin Hurda Ve Atık Teslimi", 7, 10),
        new("621", "Metal, Plastik, Lastik, Kauçuk, Kâğıt Ve Cam Hurda Ve Atıklardan Elde Edilen Hammadde Teslimi", 9, 10),
        new("622", "Pamuk, Tiftik, Yün Ve Yapağı İle Ham Post Ve Deri Teslimleri", 9, 10),
        new("623", "Ağaç Ve Orman Ürünleri Teslimi", 5, 10),
        new("624", "Yük Taşımacılığı Hizmeti", 2, 10),
        new("625", "Ticari Reklam Hizmetleri", 3, 10),
        new("626", "Diğer Teslimler", 2, 10),
        new("627", "Demir-Çelik Ürünlerinin Teslimi", 5, 10),
        new("801", "Yapım İşleri ile Bu İşlerle Birlikte İfa Edilen Mühendislik-Mimarlık ve Etüt-Proje Hizmetleri", 10, 10),
        new("802", "Etüt, Plan-Proje, Danışmanlık, Denetim ve Benzeri Hizmetler", 10, 10),
        new("803", "Makine, Teçhizat, Demirbaş ve Taşıtlara Ait Tadil, Bakım ve Onarım Hizmetleri", 10, 10),
        new("804", "Yemek Servis Hizmeti", 10, 10),
        new("805", "Organizasyon Hizmeti", 10, 10),
        new("806", "İşgücü Temin Hizmetleri", 10, 10),
        new("807", "Özel Güvenlik Hizmeti", 10, 10),
        new("808", "Yapı Denetim Hizmetleri", 10, 10),
        new("809", "Fason Olarak Yaptırılan Tekstil ve Konfeksiyon İşleri, Çanta ve Ayakkabı Dikim İşleri ve Bu İşlere Aracılık Hizmetleri", 10, 10),
        new("810", "Turistik Mağazalara Verilen Müşteri Bulma/ Götürme Hizmetleri", 10, 10),
        new("811", "Spor Kulüplerinin Yayın, Reklâm ve İsim Hakkı Gelirlerine Konu İşlemleri", 10, 10),
        new("812", "Temizlik Hizmeti", 10, 10),
        new("813", "Çevre ve Bahçe Bakım Hizmetleri", 10, 10),
        new("814", "Servis Taşımacılığı Hizmeti", 10, 10),
        new("815", "Her Türlü Baskı ve Basım Hizmetleri", 10, 10),
        new("816", "Hurda Metalden Elde Edilen Külçe Teslimleri", 10, 10),
        new("817", "Hurda Metalden Elde Edilenler Dışındaki Bakır, Çinko, Demir Çelik, Alüminyum ve Kurşun Külçe Teslimi", 10, 10),
        new("818", "Bakır, Çinko, Alüminyum ve Kurşun Ürünlerinin Teslimi", 10, 10),
        new("819", "İstisnadan Vazgeçenlerin Hurda ve Atık Teslimi", 10, 10),
        new("820", "Metal, Plastik, Lastik, Kauçuk, Kâğıt ve Cam Hurda ve Atıklardan Elde Edilen Hammadde Teslimi", 10, 10),
        new("821", "Pamuk, Tiftik, Yün ve Yapağı İle Ham Post ve Deri Teslimleri", 10, 10),
        new("822", "Ağaç ve Orman Ürünleri Teslimi", 10, 10),
        new("823", "Yük Taşımacılığı Hizmeti", 10, 10),
        new("824", "Ticari Reklam Hizmetleri", 10, 10),
        new("825", "Demir-Çelik Ürünlerinin Teslimi", 10, 10)
    ];

    private static readonly Dictionary<string, TevkifatKodu> _index = Tumu.ToDictionary(t => t.Kod);

    public static bool GecerliMi(string? kod) => kod is not null && _index.ContainsKey(kod);

    public static TevkifatKodu? Bul(string? kod) => kod is not null && _index.TryGetValue(kod, out var t) ? t : null;

    // Tevkifat oranları paydası (x/10 biçiminde) ve payı; pay payda'yı aşamaz, tam tevkifat (10/10) da geçerlidir.
    public static bool OranGecerliMi(int pay, int payda) => payda > 0 && pay > 0 && pay <= payda;
}
