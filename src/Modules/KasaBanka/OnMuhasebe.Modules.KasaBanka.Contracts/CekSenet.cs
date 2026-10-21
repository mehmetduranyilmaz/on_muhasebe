using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<EvrakTuru>))]
public enum EvrakTuru
{
    Cek,
    Senet
}

[JsonConverter(typeof(JsonStringEnumConverter<EvrakYonu>))]
public enum EvrakYonu
{
    // Müşteriden alınan (portföye giren) evrak.
    Alinan,
    // Firmanın kendi düzenleyip tedarikçiye verdiği evrak.
    Verilen
}

[JsonConverter(typeof(JsonStringEnumConverter<EvrakDurumu>))]
public enum EvrakDurumu
{
    Portfoyde,
    Tahsilde,
    Teminatta,
    CiroEdildi,
    // Karşılıksız / protestolu; evrak firmada takipte kalır.
    Karsiliksiz,
    HukukiTakipte,
    TahsilEdildi,
    // Ciro edilen evrak vadesinde ödendi; takip biter.
    CirodanKapandi,
    IadeEdildi,
    Verildi,
    Odendi,
    IadeAlindi,
    Iptal
}

[JsonConverter(typeof(JsonStringEnumConverter<EvrakIslemi>))]
public enum EvrakIslemi
{
    Giris,
    Cikis,
    TahsileVerme,
    TahsildenGeriAlma,
    Tahsil,
    ElTahsil,
    Ciro,
    CirodanTahsil,
    CirodanGeriAlma,
    CirodanKarsiliksiz,
    Karsiliksiz,
    HukukiTakip,
    KarsiliksizdanTahsil,
    KarsiliksizIade,
    Iade,
    TeminataVerme,
    TeminattanAlma,
    Odeme,
    VerilenIade,
    VerilenKarsiliksiz,
    KarsiliksizGeriAlma
}

[JsonConverter(typeof(JsonStringEnumConverter<RiskDavranisi>))]
public enum RiskDavranisi
{
    UyarVeOnayIste,
    Engelle,
    Kapali
}

[JsonConverter(typeof(JsonStringEnumConverter<VadeGruplama>))]
public enum VadeGruplama
{
    Hafta,
    Ay
}

// KarneUyariEsigi: karnede bu kadar ya da daha az boş yaprak kalınca uyarılır.
public sealed record CekSenetAyarlari(bool KesideciKimlikZorunlu, bool SenetBorcluKimlikZorunlu, RiskDavranisi RiskDavranisi, int KarneUyariEsigi = 5);

// Keşideci/borçlu: kayıtlıysa BorcluId, değilse ad + kimlik no ile girilir ve kaydedilirken kartı açılır.
public sealed record BorcluGirdisi(Guid? BorcluId, string? Ad, string? KimlikNo, string? Telefon);

public sealed record AlinanEvrakGirdisi(
    EvrakTuru Tur,
    string SeriNo,
    DateOnly VadeTarihi,
    decimal Tutar,
    string ParaBirimi,
    decimal? Kur,
    decimal? CariKuru,
    BorcluGirdisi Borclu,
    string? BankaAdi,
    string? Sube,
    string? HesapNo,
    string? KesideYeri,
    string? KefilAdi,
    string? KefilKimlikNo,
    string? Aciklama);

// Riskli keşideci/borçlu uyarısı gösterildiyse kullanıcı onayıyla RiskOnaylandi = true gönderilir.
public sealed record AlinanBordroIstegi(
    Guid CariHesapId,
    DateOnly Tarih,
    string? Aciklama,
    bool RiskOnaylandi,
    IReadOnlyList<AlinanEvrakGirdisi> Evraklar);

// Verilen çek firmanın bir banka hesabına bağlıdır ve o hesabın dövizindedir; senette döviz seçilir.
// KarneId verilirse seri karnenin aralığında, boş ve iptal edilmemiş olmalı; SeriNo boşsa karnenin sıradaki yaprağı kullanılır.
// KarneId verilmezse seri, hesabın (senette defterin) aralığına düşen karneye kendiliğinden bağlanır.
public sealed record VerilenEvrakGirdisi(
    EvrakTuru Tur,
    string SeriNo,
    DateOnly VadeTarihi,
    decimal Tutar,
    string? ParaBirimi,
    Guid? KendiHesapId,
    decimal? Kur,
    decimal? CariKuru,
    string? Aciklama,
    Guid? KarneId = null);

public sealed record VerilenBordroIstegi(Guid CariHesapId, DateOnly Tarih, string? Aciklama, IReadOnlyList<VerilenEvrakGirdisi> Evraklar);

// Bordrodaki evraklara aynı işlem uygulanır. HesapId: tahsile/teminata verilen banka ya da tahsil/ödeme hesabı.
// CariHesapId: ciro edilen cari. Tutar yalnızca tek evrakla karşılıksızdan kısmi tahsilde verilir.
public sealed record EvrakIslemIstegi(
    EvrakIslemi Islem,
    DateOnly Tarih,
    IReadOnlyList<Guid> EvrakIdleri,
    Guid? HesapId,
    Guid? CariHesapId,
    decimal? Tutar,
    decimal? Kur,
    decimal? CariKuru,
    string? Aciklama);

public sealed record BordroSonucu(Guid BordroId, string BordroNo, IReadOnlyList<Guid> EvrakIdleri);

public sealed record EvrakOzeti(
    Guid Id,
    string PortfoyNo,
    EvrakTuru Tur,
    EvrakYonu Yon,
    string SeriNo,
    DateOnly VadeTarihi,
    decimal Tutar,
    string ParaBirimi,
    EvrakDurumu Durum,
    Guid CariHesapId,
    string? CariUnvan,
    string? BorcluAdi,
    string? BankaAdi,
    string? KonumAdi,
    decimal KalanTutar,
    bool KarsiliksizMi,
    IReadOnlyList<EvrakIslemi> UygunIslemler);

public sealed record EvrakHareketDetayi(
    Guid Id,
    Guid BordroId,
    string BordroNo,
    EvrakIslemi Islem,
    DateOnly Tarih,
    EvrakDurumu? OncekiDurum,
    EvrakDurumu YeniDurum,
    Guid? HesapId,
    string? HesapAdi,
    Guid? CariHesapId,
    string? CariUnvan,
    decimal Tutar,
    string? Aciklama,
    bool GeriAlindi,
    DateTime OlusturmaTarihi);

public sealed record EvrakDetayi(
    Guid Id,
    string PortfoyNo,
    EvrakTuru Tur,
    EvrakYonu Yon,
    string SeriNo,
    DateOnly GirisTarihi,
    DateOnly VadeTarihi,
    decimal Tutar,
    string ParaBirimi,
    decimal Kur,
    EvrakDurumu Durum,
    Guid CariHesapId,
    string? CariUnvan,
    Guid? BorcluId,
    string? BorcluAdi,
    string? BorcluKimlikNo,
    string? BankaAdi,
    string? Sube,
    string? HesapNo,
    string? KesideYeri,
    string? KefilAdi,
    string? KefilKimlikNo,
    Guid? KendiHesapId,
    string? KendiHesapAdi,
    Guid? KonumHesapId,
    string? KonumHesapAdi,
    Guid? CiroCariId,
    string? CiroCariUnvan,
    decimal TahsilEdilen,
    DateOnly? KarsiliksizTarihi,
    DateTime? RiskOnayTarihi,
    string? Aciklama,
    IReadOnlyList<EvrakIslemi> UygunIslemler,
    bool GeriAlinabilirMi,
    IReadOnlyList<EvrakHareketDetayi> Hareketler);

public sealed record BorcluHesapBilgisi(Guid Id, string? BankaAdi, string? Sube, string? HesapNo);

public sealed record KarsiliksizKaydi(Guid EvrakId, string PortfoyNo, EvrakTuru Tur, DateOnly KarsiliksizTarihi, decimal Tutar, string ParaBirimi, EvrakDurumu GuncelDurum);

public sealed record BorcluOzeti(
    Guid Id,
    string Ad,
    string? KimlikNo,
    string? Telefon,
    int EvrakSayisi,
    int KarsiliksizSayisi,
    bool KaraListede,
    bool Riskli);

public sealed record BorcluDetayi(
    Guid Id,
    string Ad,
    string? KimlikNo,
    string? Telefon,
    string? Adres,
    string? Not,
    bool KaraListede,
    bool UyariKapali,
    string? RiskNotu,
    bool Riskli,
    IReadOnlyList<BorcluHesapBilgisi> Hesaplar,
    IReadOnlyList<KarsiliksizKaydi> Karsiliksizlar,
    uint Surum);

public sealed record BorcluKaydetIstegi(string Ad, string? KimlikNo, string? Telefon, string? Adres, string? Not, uint Surum = 0);

// KaraListede: elle kara listeye alma. UyariKapali: karşılıksız geçmişi olsa da uyarı verilmez (borç kapandı, güvenilir).
public sealed record RiskDurumuIstegi(bool KaraListede, bool UyariKapali, string? RiskNotu);

public sealed record RiskBilgisi(bool Riskli, string? Mesaj, IReadOnlyList<RiskliBorclu> Borclular);

public sealed record RiskliBorclu(Guid BorcluId, string Ad, string? KimlikNo, bool KaraListede, string? RiskNotu, IReadOnlyList<KarsiliksizKaydi> Karsiliksizlar);

// Cari riski evrak dövizi bazında: portföyde/tahsilde/teminatta bekleyen, ciro edilip vadesi gelmemiş (rücu riski),
// karşılıksız kalan ve carinin bizden aldığı ödenmemiş evraklar.
public sealed record CariEvrakRiski(string ParaBirimi, decimal Portfoyde, decimal CiroRiski, decimal Karsiliksiz, decimal VerilenAcik);

public sealed record VadeDilimi(DateOnly Baslangic, DateOnly Bitis, decimal AlinanTl, decimal VerilenTl, int AlinanAdet, int VerilenAdet);

public sealed record VadeTakvimi(decimal GecikmisAlinanTl, decimal GecikmisVerilenTl, int GecikmisAlinanAdet, int GecikmisVerilenAdet, IReadOnlyList<VadeDilimi> Dilimler);

public sealed record CekSenetOzeti(
    decimal PortfoyTl,
    int PortfoyAdet,
    decimal YaklasanAlinanTl,
    decimal YaklasanVerilenTl,
    int VadesiGecmisVerilenAdet,
    decimal KarsiliksizTl,
    IReadOnlyList<EvrakOzeti> Yaklasanlar);

public sealed record BordroDetayi(
    Guid Id,
    string BordroNo,
    EvrakIslemi Islem,
    DateOnly Tarih,
    Guid? CariHesapId,
    string? CariUnvan,
    Guid? HesapId,
    string? HesapAdi,
    string? Aciklama,
    IReadOnlyList<EvrakOzeti> Evraklar);

// Firmanın kendi çek karnesi (banka hesabına bağlı) ya da senet defteri. Seriler başlangıçtan ardışıktır.
public sealed record KarneEkleIstegi(EvrakTuru Tur, Guid? KendiHesapId, string BaslangicSeri, int YaprakSayisi, DateOnly Tarih, string? Aciklama);

public sealed record KarneOzeti(
    Guid Id,
    EvrakTuru Tur,
    Guid? KendiHesapId,
    string? HesapAdi,
    string BaslangicSeri,
    string BitisSeri,
    int YaprakSayisi,
    int Kullanilan,
    int IptalEdilen,
    int Kalan,
    string? SiradakiSeri,
    bool AzaldiMi,
    DateOnly Tarih,
    string? Aciklama,
    bool AktifMi);

[JsonConverter(typeof(JsonStringEnumConverter<YaprakDurumu>))]
public enum YaprakDurumu
{
    Bos,
    Kullanildi,
    Iptal
}

public sealed record KarneYapragi(string SeriNo, YaprakDurumu Durum, Guid? EvrakId, string? EvrakKodu, string? CariUnvan, decimal? Tutar,
    DateOnly? VadeTarihi, string? IptalNedeni);

public sealed record KarneDetayi(KarneOzeti Ozet, IReadOnlyList<KarneYapragi> Yapraklar);

// Yırtılan, kaybolan ya da hatalı yazılan yaprak; bir daha önerilmez.
public sealed record YaprakIptalIstegi(string SeriNo, string Neden);
