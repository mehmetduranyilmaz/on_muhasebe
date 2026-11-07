namespace OnMuhasebe.Shared.Contracts.Vergi;

// GİB e-Fatura KDV muafiyet/istisna sebep kodu (UBL-TR TaxExemptionReasonCode). KDV'si %0 olan satırda hangi kanun maddesine
// dayanıldığını gösterir; e-Fatura'da satır bazında gönderilir. Kaynak: GİB UBL-TR Kod Listeleri V1.43 (Temmuz 2026), birebir.
// Kısmi (2xx) ve tam (3xx) istisnalar ISTISNA fatura tipinde, ihraç kayıtlı (7xx) IHRACKAYITLI fatura tipinde kullanılır.
// 351 "istisna olmayan diğer": istisna değil ama %0 KDV'li belge gereken durumlar. Liste GİB duyurularıyla güncel tutulmalıdır;
// program canlıya alındıktan sonra GİB'in kaldırdığı kod silinmez, KullanimdanKalkti işaretlenir (eski belgeler açıklamasını korur).
public enum KdvIstisnaTuru
{
    Kismi,
    Tam,
    IhracKayitli
}

public sealed record KdvIstisnaKodu(string Kod, string Aciklama, KdvIstisnaTuru Tur, bool KullanimdanKalkti = false);

public static class KdvIstisnaKodlari
{
    public static IReadOnlyList<KdvIstisnaKodu> Tumu { get; } =
    [
        new("201", "17/1 Kültür ve Eğitim Amacı Taşıyan İşlemler", KdvIstisnaTuru.Kismi),
        new("202", "17/2-a Sağlık, Çevre Ve Sosyal Yardım Amaçlı İşlemler", KdvIstisnaTuru.Kismi),
        new("204", "17/2-c Yabancı Diplomatik Organ Ve Hayır Kurumlarının Yapacakları Bağışlarla İlgili Mal Ve Hizmet Alışları", KdvIstisnaTuru.Kismi),
        new("205", "17/2-d Taşınmaz Kültür Varlıklarına İlişkin Teslimler ve Mimarlık Hizmetleri", KdvIstisnaTuru.Kismi),
        new("206", "17/2-e Mesleki Kuruluşların İşlemleri", KdvIstisnaTuru.Kismi),
        new("207", "17/3 Askeri Fabrika, Tersane ve Atölyelerin İşlemleri", KdvIstisnaTuru.Kismi),
        new("208", "17/4-c Birleşme, Devir, Dönüşüm ve Bölünme İşlemleri", KdvIstisnaTuru.Kismi),
        new("209", "17/4-e Banka ve Sigorta Muameleleri Vergisi Kapsamına Giren İşlemler", KdvIstisnaTuru.Kismi),
        new("211", "17/4-h Zirai Amaçlı Su Teslimleri İle Köy Tüzel Kişiliklerince Yapılan İçme Suyu teslimleri", KdvIstisnaTuru.Kismi),
        new("212", "17/4-ı Serbest Bölgelerde Verilen Hizmetler", KdvIstisnaTuru.Kismi),
        new("213", "17/4-j Boru Hattı İle Yapılan Petrol Ve Gaz Taşımacılığı", KdvIstisnaTuru.Kismi),
        new("214", "17/4-k Organize Sanayi Bölgelerindeki Arsa ve İşyeri Teslimleri İle Konut Yapı Kooperatiflerinin Üyelerine Konut Teslimleri", KdvIstisnaTuru.Kismi),
        new("215", "17/4-l Varlık Yönetim Şirketlerinin İşlemleri", KdvIstisnaTuru.Kismi),
        new("216", "17/4-m Tasarruf Mevduatı Sigorta Fonunun İşlemleri", KdvIstisnaTuru.Kismi),
        new("217", "17/4-n Basın-Yayın ve Enformasyon Genel Müdürlüğüne Verilen Haber Hizmetleri", KdvIstisnaTuru.Kismi),
        new("218", "KDV 17/4-o md. Gümrük Antrepoları, Geçici Depolama Yerleri ile Gümrüklü Sahalarda Vergisiz Satış Yapılan İşyeri, Depo ve Ardiye Gibi Bağımsız Birimlerin Kiralanması", KdvIstisnaTuru.Kismi),
        new("219", "Hazine, Toplu Konut İdaresi Başkanlığı, Belediyeler, il özel idareleri ve yatırım izleme ve koordinasyon başkanlıklarının İşlemleri", KdvIstisnaTuru.Kismi),
        new("220", "17/4-r İki Tam Yıl Süreyle Sahip Olunan Taşınmaz ve İştirak Hisseleri ile 15/7/2023 tarihinden önce kurumların aktifinde kayıtlı Taşınmaz satışı", KdvIstisnaTuru.Kismi),
        new("221", "Geçici 15 Konut Yapı Kooperatifleri, Belediyeler ve Sosyal Güvenlik Kuruluşlarına Verilen İnşaat Taahhüt Hizmeti", KdvIstisnaTuru.Kismi),
        new("223", "Geçici 20/1 Teknoloji Geliştirme Bölgelerinde Yapılan İşlemler", KdvIstisnaTuru.Kismi),
        new("225", "Geçici 23 Milli Eğitim Bakanlığına Yapılan Bilgisayar Bağışları İle İlgili Teslimler", KdvIstisnaTuru.Kismi),
        new("226", "17/2-b Özel Okulları, Üniversite ve Yüksekokullar Tarafından Verilen Bedelsiz Eğitim Ve Öğretim Hizmetleri", KdvIstisnaTuru.Kismi),
        new("227", "17/2-b Kanunların Gösterdiği Gerek Üzerine Bedelsiz Olarak Yapılan Teslim ve Hizmetler", KdvIstisnaTuru.Kismi),
        new("228", "17/2-b Kanunun (17/1) Maddesinde Sayılan Kurum ve Kuruluşlara Bedelsiz Olarak Yapılan Teslimler", KdvIstisnaTuru.Kismi),
        new("229", "Gıda Bankacılığı Faaliyetinde Bulunan Darülacezeye, Dernek ve Vakıflara Bağışlanan, Gıda, Temizlik, Giyecek ve Yakacak Maddeleri", KdvIstisnaTuru.Kismi),
        new("230", "17/4-g Külçe Altın, Külçe Gümüş Ve Kıymetli Taşların Teslimi", KdvIstisnaTuru.Kismi),
        new("231", "17/4-g Metal Plastik, Lastik, Kauçuk, Kağıt, Cam Hurda Ve Atıkların Teslimi", KdvIstisnaTuru.Kismi),
        new("232", "17/4-g Döviz, Para, Damga Pulu, Değerli Kağıtlar, Hisse Senedi ve Tahvil Teslimleri", KdvIstisnaTuru.Kismi),
        new("233", "2942 Sayılı Kamulaştırma Kanunu Kapsamında Taşınmazların Kamulaştırmayı Yapan Devlet ve Kamu Tüzel Kişilerine Devri", KdvIstisnaTuru.Kismi),
        new("234", "17/4-ş Konut Finansmanı Amacıyla Teminat Gösterilen ve İpotek Konulan Konutların Teslimi", KdvIstisnaTuru.Kismi),
        new("235", "16/1-c Transit ve Gümrük Antrepo Rejimleri İle Geçici Depolama ve Serbest Bölge Hükümlerinin Uygulandığı Malların Teslimi", KdvIstisnaTuru.Kismi),
        new("236", "19/2 Usulüne Göre Yürürlüğe Girmiş Uluslararası Anlaşmalar Kapsamındaki İstisnalar (İade Hakkı Tanınmayan)", KdvIstisnaTuru.Kismi),
        new("237", "17/4-t 5300 Sayılı Kanuna Göre Düzenlenen Ürün Senetlerinin İhtisas/Ticaret Borsaları Aracılığıyla İlk Teslimlerinden Sonraki Teslim", KdvIstisnaTuru.Kismi),
        new("238", "17/4-u Varlıkların Varlık Kiralama Şirketlerine Devri İle Bu Varlıkların Varlık Kiralama Şirketlerince Kiralanması ve Devralınan Kuruma Devri", KdvIstisnaTuru.Kismi),
        new("239", "17/4-y Taşınmazların Finansal Kiralama Şirketlerine Devri, Finansal Kiralama Şirketi Tarafından Devredene Kiralanması ve Devri", KdvIstisnaTuru.Kismi),
        new("240", "17/4-z Patentli Veya Faydalı Model Belgeli Buluşa İlişkin Gayri Maddi Hakların Kiralanması, Devri ve Satışı", KdvIstisnaTuru.Kismi),
        new("241", "TürkAkım Gaz Boru Hattı Projesine İlişkin Anlaşmanın (9/b) Maddesinde Yer Alan Hizmetler", KdvIstisnaTuru.Kismi),
        new("242", "KDV 17/4-ö md. Gümrük Antrepoları, Geçici Depolama Yerleri ile Gümrüklü Sahalarda, İthalat ve İhracat İşlemlerine konu mallar ile transit rejim kapsamında işlem gören mallar için verilen ardiye, depolama ve terminal hizmetleri", KdvIstisnaTuru.Kismi),
        new("250", "Diğerleri", KdvIstisnaTuru.Kismi),
        new("301", "11/1-a Mal İhracatı", KdvIstisnaTuru.Tam),
        new("302", "11/1-a Hizmet İhracatı", KdvIstisnaTuru.Tam),
        new("303", "11/1-a Roaming Hizmetleri", KdvIstisnaTuru.Tam),
        new("304", "13/a Deniz Hava ve Demiryolu Taşıma Araçlarının Teslimi İle İnşa, Tadil, Bakım ve Onarımları", KdvIstisnaTuru.Tam),
        new("305", "13/b Deniz ve Hava Taşıma Araçları İçin Liman Ve Hava Meydanlarında Yapılan Hizmetler", KdvIstisnaTuru.Tam),
        new("306", "13/c Petrol Aramaları ve Petrol Boru Hatlarının İnşa ve Modernizasyonuna İlişkin Yapılan Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("307", "13/c Maden Arama, Altın, Gümüş ve Platin Madenleri İçin İşletme, Zenginleştirme Ve Rafinaj Faaliyetlerine İlişkin Teslim Ve Hizmetler", KdvIstisnaTuru.Tam),
        new("308", "13/d Teşvikli Yatırım Mallarının Teslimi", KdvIstisnaTuru.Tam),
        new("309", "13/e Liman Ve Hava Meydanlarının İnşası, Yenilenmesi Ve Genişletilmesi", KdvIstisnaTuru.Tam),
        new("310", "13/f Ulusal Güvenlik Amaçlı Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("311", "14/1 Uluslararası Taşımacılık", KdvIstisnaTuru.Tam),
        new("312", "15/a Diplomatik Organ Ve Misyonlara Yapılan Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("313", "15/b Uluslararası Kuruluşlara Yapılan Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("314", "19/2 Usulüne Göre Yürürlüğe Girmiş Uluslar Arası Anlaşmalar Kapsamındaki İstisnalar", KdvIstisnaTuru.Tam),
        new("315", "14/3 İhraç Konusu Eşyayı Taşıyan Kamyon, Çekici ve Yarı Romorklara Yapılan Motorin Teslimleri", KdvIstisnaTuru.Tam),
        new("316", "11/1-a Serbest Bölgelerdeki Müşteriler İçin Yapılan Fason Hizmetler", KdvIstisnaTuru.Tam),
        new("317", "17/4-s Engellilerin Eğitimleri, Meslekleri ve Günlük Yaşamlarına İlişkin Araç-Gereç ve Bilgisayar Programları", KdvIstisnaTuru.Tam),
        new("318", "Geçici 29 3996 Sayılı Kanuna Göre Yap-İşlet-Devret Modeli Çerçevesinde Gerçekleştirilecek Projeler, 3359 Sayılı Kanuna Göre Kiralama Karşılığı Yaptırılan Sağlık Tesislerine İlişkin Projeler ve 652 Sayılı Kanun Hükmünde Kararnameye Göre Kiralama Karşılığı Yaptırılan Eğitim Öğretim Tesislerine İlişkin Projelere İlişkin Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("319", "13/g Başbakanlık Merkez Teşkilatına Yapılan Araç Teslimleri", KdvIstisnaTuru.Tam),
        new("320", "Geçici 16 (6111 sayılı K.) İSMEP Kapsamında İstanbul İl Özel İdaresi'ne Bağlı Olarak Faaliyet Gösteren \"İstanbul Proje Koordinasyon Birimi\"ne Yapılacak Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("321", "Geçici 26 Birleşmiş Milletler (BM) ile Kuzey Atlantik Antlaşması Teşkilatı (NATO) Temsilcilikleri ve Bu Teşkilatlara Bağlı Program, Fon ve Özel İhtisas Kuruluşları ile İktisadi İşbirliği ve Kalkınma Teşkilatına (OECD) Resmi Kullanımları İçin Yapılacak Mal Teslimi ve Hizmet İfaları, Bunların Sosyal ve Ekonomik Yardım Amacıyla Bedelsiz Olarak Yapacakları Mal Teslimi ve Hizmet İfaları İle İlgili Bunlara Yapılan Mal Teslimi ve Hizmet İfaları", KdvIstisnaTuru.Tam),
        new("322", "11/1-a Türkiye'de İkamet Etmeyenlere Özel Fatura ile Yapılan Teslimler (Bavul Ticareti)", KdvIstisnaTuru.Tam),
        new("323", "13/ğ 5300 Sayılı Kanuna Göre Düzenlenen Ürün Senetlerinin İhtisas/Ticaret Borsaları Aracılığıyla İlk Teslimi", KdvIstisnaTuru.Tam),
        new("324", "13/h Türkiye Kızılay Derneğine Yapılan Teslim ve Hizmetler ile Türkiye Kızılay Derneğinin Teslim ve Hizmetleri", KdvIstisnaTuru.Tam),
        new("325", "13/ı Yem Teslimleri", KdvIstisnaTuru.Tam),
        new("326", "13/ı Gıda, Tarım ve Hayvancılık Bakanlığı Tarafından Tescil Edilmiş Gübrelerin Teslimi", KdvIstisnaTuru.Tam),
        new("327", "13/ı Gıda, Tarım ve Hayvancılık Bakanlığı Tarafından Tescil Edilmiş Gübrelerin İçeriğinde Bulunan Hammaddelerin Gübre Üreticilerine Teslimi", KdvIstisnaTuru.Tam),
        new("328", "13/i Konut veya İşyeri Teslimleri", KdvIstisnaTuru.Tam),
        new("329", "Eğitimde Fırsatları Artırma ve Teknolojiyi İyileştirme Hareketi (FATİH) projesi Kapsamında Milli Eğitim Bakanlığına Yapılacak Mal Teslimi ve Hizmet İfası", KdvIstisnaTuru.Tam),
        new("330", "KDV 13/j md. Organize Sanayi Bölgeleri ile Küçük Sanayi Sitelerinin İnşasına İlişkin Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("331", "KDV 13/m md. Ar-Ge, Yenilik ve Tasarım Faaliyetlerinde Kullanılmak Üzere Yapılan Yeni Makina ve Teçhizat Teslimlerinde İstisna", KdvIstisnaTuru.Tam),
        new("332", "KDV Geçici 39. Md. İmalat Sanayiinde Kullanılmak Üzere Yapılan Yeni Makina ve Teçhizat Teslimlerinde İstisna", KdvIstisnaTuru.Tam),
        new("333", "KDV 13/k md. Kapsamında Genel ve Özel Bütçeli Kamu İdarelerine, İl Özel İdarelerine, Belediyelere ve Köylere bağışlanan Tesislerin İnşasına İlişkin İstisna", KdvIstisnaTuru.Tam),
        new("334", "KDV 13/l md. Kapsamında Yabancılara Verilen Sağlık Hizmetlerinde İstisna", KdvIstisnaTuru.Tam),
        new("335", "KDV 13/n Basılı Kitap ve Süreli Yayınların Teslimleri", KdvIstisnaTuru.Tam),
        new("336", "Geçici 46 UEFA Müsabakaları Kapsamında Yapılacak Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("337", "Türk Akım Gaz Boru Hattı Projesine İlişkin Anlaşmanın (9/h) Maddesi Kapsamındaki Gaz Taşıma Hizmetleri", KdvIstisnaTuru.Tam),
        new("338", "İmalatçıların Mal İhracatları", KdvIstisnaTuru.Tam),
        new("339", "İmalat Sanayii ile Turizme Yönelik Yatırım Teşvik Belgesi Kapsamındaki İnşaat İşlerine İlişkin Teslim ve Hizmetler", KdvIstisnaTuru.Tam),
        new("340", "Elektrik Motorlu Taşıt Araçlarının Geliştirilmesine Yönelik Mühendislik Hizmetleri", KdvIstisnaTuru.Tam),
        new("341", "Afetzedelere Bağışlanacak Konutların İnşasına İlişkin İstisna", KdvIstisnaTuru.Tam),
        new("342", "Genel Bütçeli Kamu İdarelerine Bağışlanacak Taşınmazların İnşasına İlişkin İstisna", KdvIstisnaTuru.Tam),
        new("343", "Genel Bütçeli Kamu İdarelerine Bağışlanacak Konutların Yabancı Devlet Kurum ve Kuruluşlarına Teslimine İlişkin İstisna", KdvIstisnaTuru.Tam),
        new("344", "13/o Milli Savunma ve İç Güvenlik İhtiyaçlarında Kullanılmak Üzere Taşıt Teslimi", KdvIstisnaTuru.Tam),
        new("350", "Diğerleri", KdvIstisnaTuru.Tam),
        new("351", "KDV - İstisna Olmayan Diğer", KdvIstisnaTuru.Tam),
        new("701", "3065 s. KDV Kanununun 11/1-c md. Kapsamındaki İhraç Kayıtlı Satış", KdvIstisnaTuru.IhracKayitli),
        new("702", "DİİB ve Geçici Kabul Rejimi Kapsamındaki Satışlar", KdvIstisnaTuru.IhracKayitli),
        new("703", "4760 s. ÖTV Kanununun 8/2 Md. Kapsamındaki İhraç Kayıtlı Satış", KdvIstisnaTuru.IhracKayitli),
        new("704", "3065 sayılı KDV Kanununun (11/1-c) maddesi ve 4760 s. Ötv Kanununun 8/2. Md. Kapsamındaki İhraç Kayıtlı Satış", KdvIstisnaTuru.IhracKayitli)
    ];

    private static readonly Dictionary<string, KdvIstisnaKodu> _index = Tumu.ToDictionary(k => k.Kod);

    public static IEnumerable<KdvIstisnaKodu> Secilebilir => Tumu.Where(k => !k.KullanimdanKalkti);

    // Yeni kayıt için: kullanımdan kalkmış kod reddedilir.
    public static bool GecerliMi(string? kod) => Bul(kod) is { KullanimdanKalkti: false };

    public static KdvIstisnaKodu? Bul(string? kod) => kod is not null && _index.TryGetValue(kod, out var k) ? k : null;
}
