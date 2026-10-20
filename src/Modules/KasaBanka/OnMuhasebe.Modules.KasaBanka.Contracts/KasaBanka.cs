using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<HesapTuru>))]
public enum HesapTuru
{
    Kasa,
    Banka,
    // Firmanın kredi kartı: harcamalar çıkış olarak yazılır, bakiye eksiye (borca) gider; ekstre ödemesi virmanla kapanır.
    KrediKarti,
    // Kartla tahsilat buraya girer; bankaya aktarım virmanla, komisyon gider olarak girilir.
    Pos
}

[JsonConverter(typeof(JsonStringEnumConverter<KasaHareketTuru>))]
public enum KasaHareketTuru
{
    Acilis,
    Tahsilat,
    Odeme,
    VirmanGiris,
    VirmanCikis,
    Gelir,
    Gider,
    // Çek/senet tahsili (hesaba giriş) ve verilen evrakın ödenmesi (hesaptan çıkış).
    CekSenetTahsil,
    CekSenetOdeme
}

[JsonConverter(typeof(JsonStringEnumConverter<GelirGiderTuru>))]
public enum GelirGiderTuru
{
    Gelir,
    Gider
}

[JsonConverter(typeof(JsonStringEnumConverter<GelirGiderBicimi>))]
public enum GelirGiderBicimi
{
    // Para kasadan/bankadan çıktı (girdi). Cari seçilirse yalnızca kime ödendiğini gösterir; cari bakiyesi değişmez.
    Odendi,
    // Kasa etkilenmez; gider cariye borç (gelir cariden alacak) yazılır, ödeme/tahsilat sonra yapılır. Cari zorunludur.
    Tahakkuk
}

// Kod boş bırakılırsa türüne göre otomatik verilir (KS, BN, KK, PS + 5 hane). Surum yalnızca güncellemede gönderilir.
public sealed record HesapKaydetIstegi(
    string? Kod,
    string Ad,
    HesapTuru Tur,
    string ParaBirimi,
    string? BankaAdi,
    string? Sube,
    string? HesapNo,
    string? Iban,
    decimal? Limit,
    string? Aciklama,
    uint Surum = 0);

public sealed record HesapOzeti(
    Guid Id,
    string Kod,
    string Ad,
    HesapTuru Tur,
    string ParaBirimi,
    string? BankaAdi,
    string? Iban,
    decimal? Limit,
    bool AktifMi,
    decimal Bakiye,
    decimal BakiyeTl);

public sealed record HesapDetayi(
    Guid Id,
    string Kod,
    string Ad,
    HesapTuru Tur,
    string ParaBirimi,
    string? BankaAdi,
    string? Sube,
    string? HesapNo,
    string? Iban,
    decimal? Limit,
    string? Aciklama,
    bool AktifMi,
    decimal Bakiye,
    decimal BakiyeTl,
    bool HareketVarMi,
    uint Surum);

// Tutarlar hesabın para birimindedir. IslemId aynı işlemin bacaklarını (virman, tahsilat) gruplar.
public sealed record KasaHareketSatiri(
    Guid Id,
    Guid IslemId,
    Guid HesapId,
    DateOnly Tarih,
    KasaHareketTuru Tur,
    string? BelgeNo,
    string? Aciklama,
    decimal Giris,
    decimal Cikis,
    decimal Kur,
    decimal GirisTl,
    decimal CikisTl,
    Guid? CariHesapId,
    string? CariUnvan,
    string? KategoriAdi,
    Guid? KaynakBelgeId,
    bool IptalEdildiMi,
    bool IptalKaydiMi,
    bool IptalEdilebilirMi);

// Kur boşsa dövizli hesapta TCMB kuru kullanılır. Tutar eksi girilirse açılış çıkış olarak yazılır (ör. kredi kartı borcu).
public sealed record AcilisIstegi(Guid HesapId, DateOnly Tarih, decimal Tutar, decimal? Kur, string? Aciklama);

// Tutar hesabın dövizindedir. Cari farklı dövizdeyse (cari kartında izin verilmişse) CariKuru ile cari dövizine çevrilir.
// KapatilacakBelgeId verilirse tahsilat önce o belgenin (ör. fatura) açık kalemini kapatır; kalanı FIFO ile dağıtılır.
public sealed record TahsilatOdemeIstegi(
    Guid HesapId,
    Guid CariHesapId,
    DateOnly Tarih,
    decimal Tutar,
    decimal? Kur,
    decimal? CariKuru,
    string? BelgeNo,
    string? Aciklama,
    Guid? KapatilacakBelgeId = null);

// Tutar kaynak hesabın dövizindedir; dövizler farklıysa HedefTutar girilebilir (bankanın uyguladığı tutar), girilmezse kurlarla hesaplanır.
public sealed record VirmanIstegi(
    Guid KaynakHesapId,
    Guid HedefHesapId,
    DateOnly Tarih,
    decimal Tutar,
    decimal? HedefTutar,
    decimal? Kur,
    decimal? HedefKur,
    string? Aciklama);

// Ödendi: HesapId zorunlu, tutar hesap dövizinde. Tahakkuk: CariHesapId zorunlu, HesapId boş, tutar cari dövizinde.
public sealed record GelirGiderIstegi(
    GelirGiderTuru Tur,
    GelirGiderBicimi Bicim,
    Guid KategoriId,
    Guid? HesapId,
    Guid? CariHesapId,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    decimal Tutar,
    decimal? Kur,
    decimal? CariKuru,
    string? BelgeNo,
    string? Aciklama);

public sealed record IslemCevabi(Guid IslemId, IReadOnlyList<KasaHareketSatiri> Hareketler);

public sealed record Kategori(Guid Id, string Ad, GelirGiderTuru Tur, bool AktifMi);

public sealed record KategoriKaydetIstegi(string Ad, GelirGiderTuru Tur);

public sealed record KasaEkstreSatiri(
    Guid HareketId,
    Guid IslemId,
    DateOnly Tarih,
    KasaHareketTuru Tur,
    string? BelgeNo,
    string? Aciklama,
    string? CariUnvan,
    decimal Giris,
    decimal Cikis,
    decimal Bakiye,
    Guid? KaynakBelgeId);

public sealed record HesapEkstresi(
    Guid HesapId,
    string Kod,
    string Ad,
    string ParaBirimi,
    DateOnly Baslangic,
    DateOnly Bitis,
    decimal DevirBakiye,
    IReadOnlyList<KasaEkstreSatiri> Satirlar,
    decimal ToplamGiris,
    decimal ToplamCikis,
    decimal SonBakiye);

public sealed record KategoriTutari(Guid KategoriId, string Ad, decimal TutarTl, int Adet);

public sealed record GelirGiderRaporu(
    DateOnly Baslangic,
    DateOnly Bitis,
    IReadOnlyList<KategoriTutari> Gelirler,
    IReadOnlyList<KategoriTutari> Giderler,
    decimal ToplamGelirTl,
    decimal ToplamGiderTl);

public sealed record SonKasaHareketi(Guid HesapId, string HesapAdi, HesapTuru HesapTuru, DateOnly Tarih, KasaHareketTuru Tur, string ParaBirimi,
    decimal Giris, decimal Cikis, string? Aciklama);

// Nakit: kasa + banka + POS (TL karşılığı, kayıtlı kurlarla). Kredi kartı borcu ayrı gösterilir.
public sealed record KasaBankaOzetRaporu(
    decimal ToplamNakitTl,
    decimal KrediKartiBorcuTl,
    decimal BuAyGelirTl,
    decimal BuAyGiderTl,
    IReadOnlyList<HesapOzeti> Hesaplar,
    IReadOnlyList<KategoriTutari> BuAyGiderKategorileri,
    IReadOnlyList<SonKasaHareketi> SonHareketler);
