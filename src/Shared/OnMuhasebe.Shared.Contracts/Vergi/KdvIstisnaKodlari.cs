namespace OnMuhasebe.Shared.Contracts.Vergi;

// GİB e-Fatura KDV muafiyet/istisna sebep kodu (UBL-TR TaxExemptionReasonCode). KDV'si %0 olan satırda hangi kanun
// maddesine dayanıldığını gösterir; e-Fatura'da satır bazında gönderilir. 2xx kısmi, 3xx tam istisna, 351 istisna
// olmayan diğer (ör. KDV'ye tabi olmayan işlemler), 7xx ihraç kayıtlı satış. Liste GİB kod listesiyle güncel tutulmalıdır.
// Kayıtlarda kod metin olarak saklanır; GİB'in kaldırdığı kod listeden silinmez, KullanimdanKalkti işaretlenir: eski belgelerde
// açıklamasıyla görünmeye devam eder, yeni kayıtta seçilemez.
public sealed record KdvIstisnaKodu(string Kod, string Aciklama, bool KullanimdanKalkti = false);

public static class KdvIstisnaKodlari
{
    public static IReadOnlyList<KdvIstisnaKodu> Tumu { get; } =
    [
        new("201", "17/1 Kültür ve eğitim amacı taşıyan işlemler"),
        new("202", "17/2-a Sağlık, çevre ve sosyal yardım amaçlı işlemler"),
        new("205", "17/2-d Taşınmaz kültür varlıklarına ilişkin teslimler ve mimarlık hizmetleri"),
        new("206", "17/2-e Mesleki kuruluşların işlemleri"),
        new("209", "17/4-e Banka ve sigorta muameleleri vergisi kapsamına giren işlemler"),
        new("211", "17/4-h Zirai amaçlı su teslimleri ile köy tüzel kişiliklerince yapılan içme suyu teslimleri"),
        new("212", "17/4-ı Serbest bölgelerde verilen hizmetler"),
        new("213", "17/4-j Boru hattı ile yapılan petrol ve gaz taşımacılığı"),
        new("214", "17/4-k OSB'lerdeki arsa ve işyeri teslimleri ile konut yapı kooperatiflerinin üyelerine konut teslimleri"),
        new("220", "17/4-r İki tam yıl süreyle sahip olunan taşınmaz ve iştirak hisseleri satışları"),
        new("223", "Geçici 20/1 Teknoloji geliştirme bölgelerinde yapılan işlemler"),
        new("301", "11/1-a Mal ihracatı"),
        new("302", "11/1-a Hizmet ihracatı"),
        new("303", "11/1-a Roaming hizmetleri"),
        new("304", "13/a Deniz, hava ve demiryolu taşıma araçlarının teslimi"),
        new("305", "13/b Deniz ve hava taşıma araçları için liman ve hava meydanlarında yapılan hizmetler"),
        new("306", "13/c Petrol aramaları ve petrol boru hatlarına ilişkin teslim ve hizmetler"),
        new("307", "13/c Maden arama faaliyetinde bulunanlara yapılan teslim ve hizmetler"),
        new("308", "13/d Teşvikli yatırım mallarının teslimi"),
        new("309", "13/e Liman ve hava meydanlarının inşası, yenilenmesi ve genişletilmesi"),
        new("310", "13/f Ulusal güvenlik amaçlı teslim ve hizmetler"),
        new("311", "14/1 Uluslararası taşımacılık"),
        new("312", "15/a Diplomatik organ ve misyonlara yapılan teslim ve hizmetler"),
        new("313", "15/b Uluslararası kuruluşlara yapılan teslim ve hizmetler"),
        new("314", "19/2 Bedelsiz ihracat"),
        new("315", "14/3 İhraç amaçlı serbest bölge hizmetleri"),
        new("316", "11/1-c Serbest bölgelerdeki müşteriler için yapılan fason hizmetler"),
        new("317", "17/4-s Engellilerin eğitim, meslek ve günlük yaşamlarına ilişkin araç-gereç ve bilgisayar programları"),
        new("322", "11/1-a Türkiye'de ikamet etmeyenlere özel fatura ile yapılan teslimler (bavul ticareti)"),
        new("323", "13/ğ Ürün senetlerinin ihtisas/ticaret borsaları aracılığıyla ilk teslimi"),
        new("324", "13/h Türkiye Kızılay Derneği'ne yapılan teslim ve hizmetler"),
        new("325", "13/ı Yem teslimleri"),
        new("326", "13/ı Tescilli gübrelerin teslimi"),
        new("328", "13/i Konut veya işyeri teslimleri"),
        new("330", "13/j OSB ve küçük sanayi sitelerinin inşasına ilişkin teslim ve hizmetler"),
        new("331", "13/m Ar-Ge, yenilik ve tasarım faaliyetlerinde kullanılacak makine ve teçhizat teslimleri"),
        new("334", "13/k Yabancılara verilen sağlık hizmetleri"),
        new("335", "13/n Basılı kitap ve süreli yayınların teslimleri"),
        new("350", "Diğer istisnalar"),
        new("351", "İstisna olmayan diğer"),
        new("701", "11/1-c İhraç kayıtlı satış"),
        new("702", "DİİB ve geçici kabul rejimi kapsamındaki satışlar"),
        new("703", "ÖTV 8/2 kapsamındaki ihraç kayıtlı satış")
    ];

    private static readonly Dictionary<string, KdvIstisnaKodu> _index = Tumu.ToDictionary(k => k.Kod);

    public static IEnumerable<KdvIstisnaKodu> Secilebilir => Tumu.Where(k => !k.KullanimdanKalkti);

    // Yeni kayıt için: kullanımdan kalkmış kod reddedilir.
    public static bool GecerliMi(string? kod) => Bul(kod) is { KullanimdanKalkti: false };

    public static KdvIstisnaKodu? Bul(string? kod) => kod is not null && _index.TryGetValue(kod, out var k) ? k : null;
}
