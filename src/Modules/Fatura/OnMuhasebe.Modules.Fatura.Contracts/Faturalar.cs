using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Fatura.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<FaturaTuru>))]
public enum FaturaTuru
{
    Satis,
    Alis,
    SatistanIade,
    AlistanIade,
    // Vergi mükellefi olmayandan alım için biz düzenleriz (KDV yok, stopaj var).
    GiderPusulasi,
    // Serbest meslek erbabının düzenlediği belge; biz alırız (KDV + gelir vergisi stopajı).
    SerbestMeslekMakbuzu
}

// Satış ve satıştan iade faturalarında; fiyat önerisini, numara serisini ve nihai tüketiciye kesilebilmeyi belirler.
[JsonConverter(typeof(JsonStringEnumConverter<SatisSekli>))]
public enum SatisSekli
{
    Toptan,
    Perakende
}

[JsonConverter(typeof(JsonStringEnumConverter<FaturaDurumu>))]
public enum FaturaDurumu
{
    Taslak,
    Onayli,
    Iptal
}

[JsonConverter(typeof(JsonStringEnumConverter<GenelIskontoTuru>))]
public enum GenelIskontoTuru
{
    Yuzde,
    Tutar
}

public static class FaturaTurleri
{
    // Numarasını firmanın verdiği (bizim düzenlediğimiz) belgeler; diğerlerinde karşı tarafın numarası girilir.
    // Gider pusulasını da biz düzenleriz (kendi serisiyle numaralanır); SMM'yi karşı taraf düzenler.
    public static bool GidenMi(FaturaTuru tur) => tur is FaturaTuru.Satis or FaturaTuru.AlistanIade or FaturaTuru.GiderPusulasi;

    public static bool SatisTarafiMi(FaturaTuru tur) => tur is FaturaTuru.Satis or FaturaTuru.SatistanIade;

    // Peşin işlemde paranın yönü: satışta ve alıştan iadede para bize girer; alış/GP/SMM'de bizden çıkar.
    public static bool ParaGirisiMi(FaturaTuru tur) => tur is FaturaTuru.Satis or FaturaTuru.AlistanIade;

    // Gider pusulası KDV mükellefi olmayana düzenlenir: satırlarda KDV olmaz.
    public static bool KdvsizMi(FaturaTuru tur) => tur is FaturaTuru.GiderPusulasi;

    // Gelir vergisi stopajı (tevkifatı) uygulanabilen belgeler.
    public static bool StopajliMi(FaturaTuru tur) => tur is FaturaTuru.GiderPusulasi or FaturaTuru.SerbestMeslekMakbuzu;

    // Mal/hizmet satırları stok hareketi oluşturabilir mi (alış tarafı gibi). SMM yalnızca hizmettir.
    public static bool StoklaIliskiliMi(FaturaTuru tur) =>
        tur is FaturaTuru.Satis or FaturaTuru.Alis or FaturaTuru.SatistanIade or FaturaTuru.AlistanIade or FaturaTuru.GiderPusulasi;
}

// Cari seçilmeyen perakende fatura nihai tüketiciye kesilir; bilgiler boşsa "Nihai Tüketici" / 11111111111 yazılır.
public sealed record NihaiTuketiciBilgisi(string? AdSoyad, string? Tckn);

// Stok kartı seçilmezse serbest satırdır (Ad zorunlu, stok hareketi oluşmaz). Stoklu satırda Ad boşsa kart adı kullanılır.
// Depo boşsa varsayılan depo. Fiyat seçilen birim içindir; KdvDahilMi ise girilen fiyat KDV dahildir. İskontolar yüzdedir.
// TevkifatKodu/Pay/Payda opsiyoneldir; verilmezse satırda KDV tevkifatı yoktur.
// IrsaliyeSatiriId verilirse satır irsaliyeden aktarılmıştır: stok hareketi irsaliyede olduğu için fatura stok yazmaz;
// kart, birim ve depo irsaliye satırıyla aynı olmalı, miktar faturalanmamış kalanı aşamaz.
public sealed record FaturaSatiriIstegi(
    Guid? StokKartiId,
    string? Ad,
    Guid? DepoId,
    string Birim,
    decimal Miktar,
    decimal BirimFiyat,
    bool KdvDahilMi,
    int KdvOrani,
    decimal Iskonto1,
    decimal Iskonto2,
    decimal Iskonto3,
    string? TevkifatKodu = null,
    int TevkifatPay = 0,
    int TevkifatPayda = 0,
    Guid? IrsaliyeSatiriId = null);

// PesinHesapId verilirse fatura kesinleşirken o kasa/banka/POS hesabından peşin tahsil edilir (ödenir).
// Numara yalnızca gelen faturalarda (alış, satıştan iade) girilir; giden faturaların numarası otomatik verilir.
// Kur boşsa dövizli faturada TCMB kuru kullanılır. Surum yalnızca güncellemede gönderilir.
public sealed record FaturaKaydetIstegi(
    FaturaTuru Tur,
    SatisSekli? SatisSekli,
    Guid? CariHesapId,
    NihaiTuketiciBilgisi? NihaiTuketici,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    string? Numara,
    decimal? Kur,
    GenelIskontoTuru GenelIskontoTuru,
    decimal GenelIskonto,
    string? Aciklama,
    IReadOnlyList<FaturaSatiriIstegi> Satirlar,
    uint Surum = 0,
    Guid? PesinHesapId = null,
    decimal StopajOrani = 0m);

public sealed record FaturaOzeti(
    Guid Id,
    FaturaTuru Tur,
    SatisSekli? SatisSekli,
    FaturaDurumu Durum,
    string? Numara,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    Guid? CariHesapId,
    string CariUnvan,
    string ParaBirimi,
    decimal GenelToplam,
    decimal GenelToplamTl,
    decimal? Kalan);

public sealed record FaturaSatiriDetayi(
    Guid Id,
    int Sira,
    Guid? StokKartiId,
    string? StokKodu,
    string Ad,
    bool StokluMu,
    Guid? DepoId,
    string Birim,
    decimal Miktar,
    decimal BirimFiyat,
    bool KdvDahilMi,
    decimal Iskonto1,
    decimal Iskonto2,
    decimal Iskonto3,
    int KdvOrani,
    decimal BrutTutar,
    decimal IskontoTutari,
    decimal GenelIskontoPayi,
    decimal Matrah,
    decimal KdvTutari,
    decimal Toplam,
    string? TevkifatKodu,
    int TevkifatPay,
    int TevkifatPayda,
    decimal TevkifEdilenKdv,
    Guid? IrsaliyeSatiriId);

// Cari bilgileri fatura anındaki kopyadır; cari kartı sonradan değişse de fatura değişmez.
public sealed record FaturaDetayi(
    Guid Id,
    FaturaTuru Tur,
    SatisSekli? SatisSekli,
    FaturaDurumu Durum,
    string? Numara,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    Guid? CariHesapId,
    string? CariKodu,
    string CariUnvan,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres,
    string ParaBirimi,
    decimal Kur,
    GenelIskontoTuru GenelIskontoTuru,
    decimal GenelIskonto,
    string? Aciklama,
    decimal BrutToplam,
    decimal SatirIskontoToplami,
    decimal GenelIskontoTutari,
    decimal Matrah,
    decimal KdvToplami,
    decimal GenelToplam,
    decimal GenelToplamTl,
    decimal StopajOrani,
    decimal StopajTutari,
    decimal TevkifEdilenKdvToplami,
    decimal OdenecekTutar,
    IReadOnlyList<KdvDagilimi> KdvDagilimi,
    IReadOnlyList<FaturaSatiriDetayi> Satirlar,
    Guid Ettn,
    DateTime? OnayTarihi,
    DateTime? IptalTarihi,
    uint Surum,
    Guid? PesinHesapId,
    decimal? Kalan,
    IReadOnlyList<BagliBelge> Irsaliyeler);

// Eksi stok ayarı "Uyar" ise kayıt yapılır ve uyarı döner.
public sealed record FaturaCevabi(FaturaDetayi Fatura, string? Uyari);

[JsonConverter(typeof(JsonStringEnumConverter<KayitAkisi>))]
public enum KayitAkisi
{
    // Kaydet = fatura kesinleşir, cari/stok hareketleri oluşur; düzenleme hareketleri ters kayıtla yeniler.
    DogrudanKayit,
    // Taslak serbestçe düzenlenir; onaylanınca kesinleşir ve onaylı fatura yalnızca iptal edilebilir.
    TaslakOnay
}

// Seriler GİB biçiminde 3 karakterdir (A-Z, 0-9). KDV dahil ve vade günü yeni faturanın varsayılanlarıdır.
public sealed record FaturaAyarlari(
    KayitAkisi KayitAkisi,
    string ToptanSeri,
    string PerakendeSeri,
    string IadeSeri,
    bool ToptanKdvDahil,
    bool PerakendeKdvDahil,
    int VarsayilanVadeGunu,
    string GiderPusulasiSeri = "GPS",
    string IrsaliyeSeri = "IRS");

// Tutarlar TL ve KDV hariçtir; iadeler düşülmüştür.
public sealed record AylikFaturaTutari(int Yil, int Ay, decimal Satis, decimal Alis);

public sealed record FaturaOzetRaporu(
    decimal BuAySatis,
    decimal BuAyAlis,
    decimal BuAyHesaplananKdv,
    decimal BuAyIndirilecekKdv,
    int BuAyFaturaSayisi,
    int TaslakSayisi,
    IReadOnlyList<AylikFaturaTutari> SonAylar,
    IReadOnlyList<FaturaOzeti> SonFaturalar);
