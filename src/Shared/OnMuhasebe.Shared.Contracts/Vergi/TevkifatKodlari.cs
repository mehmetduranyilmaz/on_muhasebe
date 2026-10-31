namespace OnMuhasebe.Shared.Contracts.Vergi;

// GİB kısmi KDV tevkifatı kodu ve varsayılan oranı (pay/payda, ör. 7/10). Oranlar tebliğle değiştiği için
// burada yalnızca varsayılan tutulur: faturada satır bazında pay/payda saklanır ve kullanıcı gerektiğinde
// oranı değiştirebilir. Yürürlükteki tebliğe göre liste ve oranlar güncellenmelidir.
public sealed record TevkifatKodu(string Kod, string Aciklama, int Pay, int Payda);

public static class TevkifatKodlari
{
    public static IReadOnlyList<TevkifatKodu> Tumu { get; } =
    [
        new("601", "Yapım işleri ile bu işlerle birlikte ifa edilen mühendislik-mimarlık ve etüt-proje hizmetleri", 4, 10),
        new("602", "Etüt, plan-proje, danışmanlık, denetim ve benzeri hizmetler", 9, 10),
        new("603", "Makine, teçhizat, demirbaş ve taşıtlara ait tadil, bakım ve onarım hizmetleri", 7, 10),
        new("604", "Yemek servis hizmeti", 5, 10),
        new("605", "Organizasyon hizmeti", 5, 10),
        new("606", "İşgücü temin hizmetleri (özel güvenlik dâhil)", 9, 10),
        new("607", "Yapı denetim hizmetleri", 9, 10),
        new("608", "Fason tekstil ve konfeksiyon, çanta ve ayakkabı dikim işleri", 7, 10),
        new("609", "Turistik mağazalara verilen müşteri bulma/götürme hizmetleri", 9, 10),
        new("610", "Spor kulüplerinin yayın, reklam ve isim hakkı gelirlerine konu işlemleri", 9, 10),
        new("611", "Temizlik, çevre ve bahçe bakım hizmetleri", 9, 10),
        new("612", "Servis taşımacılığı hizmeti", 5, 10),
        new("613", "Her türlü baskı ve basım hizmetleri", 7, 10),
        new("617", "Diğer hizmetler (KDV mükellefleri ile belirlenmiş alıcılara ifa edilen)", 5, 10),
        new("620", "Ticari reklam hizmetleri", 3, 10),
        new("801", "Külçe metal teslimleri", 7, 10),
        new("802", "Bakır, çinko, alüminyum ve kurşun ürünlerinin teslimi", 7, 10),
        new("803", "Metal, plastik, lastik, kauçuk, kâğıt, cam hurda ve atıkları (istisnadan vazgeçilmesi hâlinde)", 7, 10),
        new("805", "Pamuk, tiftik, yün, yapağı ile ham post ve deri teslimleri", 9, 10),
        new("806", "Ağaç ve orman ürünleri teslimi", 5, 10),
        new("807", "Demir-çelik ürünlerinin teslimi", 5, 10)
    ];

    private static readonly Dictionary<string, TevkifatKodu> _index = Tumu.ToDictionary(t => t.Kod);

    public static bool GecerliMi(string? kod) => kod is not null && _index.ContainsKey(kod);

    public static TevkifatKodu? Bul(string? kod) => kod is not null && _index.TryGetValue(kod, out var t) ? t : null;

    // Tevkifat oranları paydası (x/10 biçiminde) ve payı; pay payda'yı aşamaz, tam tevkifat (10/10) da geçerlidir.
    public static bool OranGecerliMi(int pay, int payda) => payda > 0 && pay > 0 && pay <= payda;
}
