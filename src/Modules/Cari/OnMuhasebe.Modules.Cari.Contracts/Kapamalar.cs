using OnMuhasebe.Shared.Contracts.Sayfalama;

namespace OnMuhasebe.Modules.Cari.Contracts;

// Kapanmamış (tamamı ya da bir kısmı açık) cari kalem. Tutarlar carinin para birimindedir.
// GecikmeGun: rapor tarihine göre vadeden (vade yoksa işlem tarihinden) geçen gün; vadesi gelmemişse 0 veya eksi.
public sealed record AcikKalem(
    Guid HareketId,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    CariHareketTuru Tur,
    HareketYonu Yon,
    string? BelgeNo,
    string? Aciklama,
    decimal Tutar,
    decimal Kapanan,
    decimal Kalan,
    int GecikmeGun,
    bool ElleKapamaMi,
    Guid? KaynakBelgeId,
    bool AvansMi = false);

// Bir kalemin hangi karşı kalemlerle (ne kadar) kapandığı.
public sealed record KapamaSatiri(
    Guid KarsiHareketId,
    DateOnly Tarih,
    CariHareketTuru Tur,
    string? BelgeNo,
    decimal Tutar,
    Guid? KaynakBelgeId);

public sealed record KapamaDetayi(
    Guid HareketId,
    HareketYonu Yon,
    decimal Tutar,
    decimal Kalan,
    bool ElleKapamaMi,
    IReadOnlyList<KapamaSatiri> Kapamalar);

public sealed record ElleKapamaOgesi(Guid KarsiHareketId, decimal Tutar);

// Kalemin kapamaları bu listeyle değiştirilir ve kalem otomatik (FIFO) kapamadan çıkarılır.
public sealed record ElleKapamaIstegi(IReadOnlyList<ElleKapamaOgesi> Kapamalar);

// Belgenin (ör. fatura) ya da tek bir hareketin açık kalanı, carinin açık sipariş avanslarından (en eskiden başlayarak) mahsup edilir.
// Tutar boşsa açık kalan ya da avans hangisi küçükse o kadar.
public sealed record AvansMahsubuIstegi(Guid? BelgeId, Guid? HareketId, decimal? Tutar);

public sealed record AvansMahsubuSonucu(decimal MahsupEdilen, decimal KalanAvans);

// Carinin mahsup edilmemiş sipariş avansları (cari dövizinde): alınan (müşteriden) ve verilen (tedarikçiye).
public sealed record CariAvansDurumu(string ParaBirimi, decimal AlinanAvans, decimal VerilenAvans);

// Vade analizi: açık kalemlerin gecikmeye göre dağılımı.
public sealed record VadeDilimleri(decimal VadesiGelmemis, decimal Gun0Ile30, decimal Gun31Ile60, decimal Gun61Ile90, decimal Gun90Ustu)
{
    public decimal Toplam => VadesiGelmemis + Gun0Ile30 + Gun31Ile60 + Gun61Ile90 + Gun90Ustu;
    public decimal VadesiGecen => Gun0Ile30 + Gun31Ile60 + Gun61Ile90 + Gun90Ustu;
}

public sealed record VadeAnaliziSatiri(Guid CariId, string Kod, string Unvan, string ParaBirimi, VadeDilimleri Dilimler, int EnUzunGecikmeGun);

// Alacaklar: carilerin bize borçlu olduğu açık kalemler; değilse bizim carilere borcumuz. Satırlar vadesi geçen tutara göre sıralı ve sayfalıdır;
// TlToplami özet kartları içindir: tüm cariler, dövizliler rapor tarihinin TCMB döviz alış kuruyla TL'ye çevrilmiş (sayfadan bağımsız).
// AvansTl: dilimlere girmeyen, mahsup edilmemiş avanslar (alacaklarda alınan, borçlarda verilen). KurBulunamayanlar toplamlara katılamayan dövizlerdir.
public sealed record VadeAnalizi(
    DateOnly Tarih,
    bool Alacaklar,
    VadeDilimleri TlToplami,
    SayfaliSonuc<VadeAnaliziSatiri> Satirlar,
    decimal AvansTl,
    IReadOnlyList<string> KurBulunamayanlar);
