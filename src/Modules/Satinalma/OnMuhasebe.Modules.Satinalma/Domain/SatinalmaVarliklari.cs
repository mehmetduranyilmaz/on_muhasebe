using OnMuhasebe.Modules.Satinalma.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Satinalma.Domain;

internal static class TalepHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("talep_bulunamadi", "Talep bulunamadı.");
    public static readonly Hata Degistirilemez = Hata.IsKurali("talep_degistirilemez", "Yalnız taslak ya da reddedilen talep değiştirilebilir.");
    public static readonly Hata Gonderilemez = Hata.IsKurali("talep_gonderilemez", "Yalnız taslak ya da reddedilen talep onaya gönderilebilir.");
    public static readonly Hata OnaydaDegil = Hata.IsKurali("talep_onayda_degil", "Talep onay aşamasında değil.");
    public static readonly Hata KendiTalebi = Hata.IsKurali("kendi_talebi", "Kendi talebinizi onaylayamaz ya da reddedemezsiniz.");
    public static readonly Hata SiraSizdeDegil = Hata.Yetki("onay_sirasi", "Bu talebin sıradaki onayı size ait değil.");
    public static readonly Hata RedGerekcesi = Hata.Dogrulama("red_gerekcesi", "Reddetmek için gerekçe yazın.");
    public static readonly Hata OnaylanmadI = Hata.IsKurali("talep_onaylanmadi", "Yalnız onaylanmış talep siparişe dönüştürülebilir.");
    public static readonly Hata IptalEdilemez = Hata.IsKurali("talep_iptal_edilemez", "Siparişe dönüşmüş ya da iptal edilmiş talep iptal edilemez.");
    public static readonly Hata KalemGerekli = Hata.Dogrulama("kalem_gerekli", "En az bir kalem ekleyin.");
    public static readonly Hata KalemAdiGerekli = Hata.Dogrulama("kalem_adi", "Stok kartı seçilmeyen kalemde ad ve birim gerekli.");
    public static readonly Hata StokKartiGecersiz = Hata.Dogrulama("stok_karti", "Kalemin stok kartı bulunamadı ya da pasif.");
    public static readonly Hata StokBirimGecersiz = Hata.Dogrulama("stok_birim", "Kalemin birimi stok kartında tanımlı değil.");
    public static readonly Hata StokLisansiYok = Hata.IsKurali("stok_lisansi", "Stok kartı seçmek için Stok modülü gerekli; kalemi serbest yazabilirsiniz.");
    public static readonly Hata FaturaLisansiYok = Hata.IsKurali("fatura_lisansi", "Siparişe dönüştürmek için Fatura modülü gerekli.");
    public static readonly Hata SiparisYetkisiYok = Hata.Yetki("yetki_yok", "Sipariş açma yetkiniz yok.");
    public static readonly Hata MasrafMerkeziGecersiz = Hata.Dogrulama("masraf_merkezi", "Masraf merkezi bulunamadı ya da pasif.");
    public static readonly Hata BasamakGecersiz = Hata.Dogrulama("basamak_gecersiz", "Her basamakta rol ya da kişiden yalnız biri seçilmelidir.");
    public static readonly Hata RolGecersiz = Hata.Dogrulama("rol_gecersiz", "Seçilen rol bu firmada bulunamadı.");
    public static readonly Hata KullaniciGecersiz = Hata.Dogrulama("kullanici_gecersiz", "Seçilen kişi bu firmanın aktif kullanıcısı değil.");

    public static Hata OnaylayanYok(string basamak) => Hata.IsKurali("onaylayan_yok",
        $"\"{basamak}\" basamağında talep edenden başka onaylayabilecek kişi yok. Onay kuralını ya da rollerin onay yetkisini düzenleyin.");
}

// Talep edilen mal ya da hizmet. Onaydan önce muhasebe ya da stok etkisi yoktur; onaylanınca taslak satınalma siparişine dönüşür.
internal sealed class SatinalmaTalebi : FirmaEntity, ISilinebilir, IIsyeriVarligi
{
    private readonly List<TalepKalemi> _kalemler = [];
    private readonly List<TalepOnayi> _onaylar = [];

    private SatinalmaTalebi() { }

    public string Kod { get; private set; } = null!;
    public Guid TalepEdenId { get; private set; }
    public string TalepEden { get; private set; } = null!;
    public DateOnly Tarih { get; private set; }
    public DateOnly? GerekliTarih { get; private set; }
    public string Gerekce { get; private set; } = null!;
    public Guid? IsyeriId { get; private set; }
    public Guid? MasrafMerkeziId { get; private set; }
    public TalepDurumu Durum { get; private set; }
    public decimal Toplam { get; private set; }
    public int GonderimNo { get; private set; }
    public Guid? SiparisId { get; private set; }
    public DateTime? KararTarihi { get; private set; }
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public IReadOnlyList<TalepKalemi> Kalemler => _kalemler;

    public IReadOnlyList<TalepOnayi> Onaylar => _onaylar;

    // Sıradaki onay: son gönderimin bekleyen ilk basamağı.
    public TalepOnayi? BekleyenOnay => Durum == TalepDurumu.Onayda
        ? _onaylar.Where(o => o.GonderimNo == GonderimNo && o.Durum == OnayDurumu.Bekliyor).OrderBy(o => o.Sira).FirstOrDefault()
        : null;

    public static Sonuc<SatinalmaTalebi> Olustur(string kod, Guid talepEdenId, string talepEden, TalepKaydetIstegi i)
    {
        var talep = new SatinalmaTalebi { Kod = kod, TalepEdenId = talepEdenId, TalepEden = talepEden, Durum = TalepDurumu.Taslak };
        var sonuc = talep.Ayarla(i);
        return sonuc.BasariliMi ? talep : sonuc.Hata;
    }

    public Sonuc Guncelle(TalepKaydetIstegi i) =>
        Durum is TalepDurumu.Taslak or TalepDurumu.Reddedildi ? Ayarla(i) : TalepHatalari.Degistirilemez;

    private Sonuc Ayarla(TalepKaydetIstegi i)
    {
        if (i.Kalemler.Count == 0)
        {
            return TalepHatalari.KalemGerekli;
        }

        Tarih = i.Tarih;
        GerekliTarih = i.GerekliTarih;
        Gerekce = i.Gerekce.Trim();
        IsyeriId = i.IsyeriId;
        MasrafMerkeziId = i.MasrafMerkeziId;
        _kalemler.Clear();
        foreach (var k in i.Kalemler)
        {
            _kalemler.Add(TalepKalemi.Olustur(Id, k));
        }

        Toplam = _kalemler.Sum(k => k.Tutar);
        return Sonuc.Basarili();
    }

    // Kural değişse de yoldaki talep gönderildiği andaki basamaklarla ilerler. Her gönderim yeni turdur, eski kararlar geçmişte kalır.
    public Sonuc OnayaGonder(IReadOnlyList<OnayBasamagi> basamaklar)
    {
        if (Durum is not (TalepDurumu.Taslak or TalepDurumu.Reddedildi))
        {
            return TalepHatalari.Gonderilemez;
        }

        GonderimNo++;
        var sira = 1;
        foreach (var b in basamaklar)
        {
            _onaylar.Add(TalepOnayi.Olustur(Id, GonderimNo, sira++, b.RolId, b.KullaniciId, b.Ad));
        }

        Durum = TalepDurumu.Onayda;
        KararTarihi = null;
        return Sonuc.Basarili();
    }

    // yetkiliMi: kullanıcının sıradaki basamağa yetkisi. Kendi talebi yetkiden önce reddedilir.
    public Sonuc Karar(Guid kullaniciId, string kullaniciAdi, bool yetkiliMi, bool onayMi, string? not, DateTime simdi)
    {
        if (BekleyenOnay is not { } sira)
        {
            return TalepHatalari.OnaydaDegil;
        }

        if (kullaniciId == TalepEdenId)
        {
            return TalepHatalari.KendiTalebi;
        }

        if (!yetkiliMi)
        {
            return TalepHatalari.SiraSizdeDegil;
        }

        if (!onayMi && string.IsNullOrWhiteSpace(not))
        {
            return TalepHatalari.RedGerekcesi;
        }

        sira.Karar(onayMi, kullaniciId, kullaniciAdi, not, simdi);
        if (!onayMi)
        {
            Durum = TalepDurumu.Reddedildi;
            KararTarihi = simdi;
        }
        else if (BekleyenOnay is null)
        {
            Durum = TalepDurumu.Onaylandi;
            KararTarihi = simdi;
        }

        return Sonuc.Basarili();
    }

    public Sonuc IptalEt()
    {
        if (Durum is TalepDurumu.SipariseDonustu or TalepDurumu.Iptal)
        {
            return TalepHatalari.IptalEdilemez;
        }

        Durum = TalepDurumu.Iptal;
        return Sonuc.Basarili();
    }

    // Sipariş iptal edilmişse (siparisGecerliMi false) talep yeniden dönüştürülebilir.
    public Sonuc SipariseDonusebilirMi(bool siparisGecerliMi) =>
        Durum == TalepDurumu.Onaylandi || (Durum == TalepDurumu.SipariseDonustu && !siparisGecerliMi) ? Sonuc.Basarili() : TalepHatalari.OnaylanmadI;

    public void SipariseDonustu(Guid siparisId)
    {
        SiparisId = siparisId;
        Durum = TalepDurumu.SipariseDonustu;
    }
}

internal sealed class TalepKalemi : FirmaEntity
{
    private TalepKalemi() { }

    public Guid TalepId { get; private set; }
    public Guid? StokKartiId { get; private set; }
    public string Ad { get; private set; } = null!;
    public string Birim { get; private set; } = null!;
    public decimal Miktar { get; private set; }
    public decimal TahminiBirimFiyat { get; private set; }
    public int KdvOrani { get; private set; }
    public decimal Tutar { get; private set; }

    public static TalepKalemi Olustur(Guid talepId, TalepKalemiIstegi i) => new()
    {
        TalepId = talepId,
        StokKartiId = i.StokKartiId,
        Ad = (i.Ad ?? "").Trim(),
        Birim = (i.Birim ?? "").Trim(),
        Miktar = i.Miktar,
        TahminiBirimFiyat = i.TahminiBirimFiyat,
        KdvOrani = i.KdvOrani,
        Tutar = Para.Yuvarla(i.Miktar * i.TahminiBirimFiyat)
    };
}

// Gönderim anındaki onay basamağı ve kararı. Ad, kural sonradan değişse de geçmişte okunur.
internal sealed class TalepOnayi : FirmaEntity
{
    private TalepOnayi() { }

    public Guid TalepId { get; private set; }
    public int GonderimNo { get; private set; }
    public int Sira { get; private set; }
    public Guid? RolId { get; private set; }
    public Guid? KullaniciId { get; private set; }
    public string Onaylayan { get; private set; } = null!;
    public OnayDurumu Durum { get; private set; }
    public Guid? KararVerenId { get; private set; }
    public string? KararVeren { get; private set; }
    public DateTime? KararTarihi { get; private set; }
    public string? Not { get; private set; }

    public static TalepOnayi Olustur(Guid talepId, int gonderimNo, int sira, Guid? rolId, Guid? kullaniciId, string onaylayan) => new()
    {
        TalepId = talepId,
        GonderimNo = gonderimNo,
        Sira = sira,
        RolId = rolId,
        KullaniciId = kullaniciId,
        Onaylayan = onaylayan,
        Durum = OnayDurumu.Bekliyor
    };

    public void Karar(bool onayMi, Guid kararVerenId, string kararVeren, string? not, DateTime simdi)
    {
        Durum = onayMi ? OnayDurumu.Onaylandi : OnayDurumu.Reddedildi;
        KararVerenId = kararVerenId;
        KararVeren = kararVeren;
        KararTarihi = simdi;
        Not = string.IsNullOrWhiteSpace(not) ? null : not.Trim();
    }
}

internal sealed class OnayBasamagi : FirmaEntity
{
    private OnayBasamagi() { }

    public int Sira { get; private set; }
    public decimal EnAzTutar { get; private set; }
    public Guid? RolId { get; private set; }
    public Guid? KullaniciId { get; private set; }
    public string Ad { get; private set; } = null!;

    public static OnayBasamagi Olustur(int sira, decimal enAzTutar, Guid? rolId, Guid? kullaniciId, string ad) =>
        new() { Sira = sira, EnAzTutar = enAzTutar, RolId = rolId, KullaniciId = kullaniciId, Ad = ad };
}

internal static class OnayKurallari
{
    // Tutar basamağın alt sınırına ulaştıysa basamak uygulanır: büyük tutar küçüklerin de onayından geçer.
    public static List<OnayBasamagi> Uygulanan(IEnumerable<OnayBasamagi> basamaklar, decimal toplam) =>
        basamaklar.Where(b => toplam >= b.EnAzTutar).OrderBy(b => b.Sira).ToList();

    // Rol basamağında o rol, kişi basamağında o kişi. Tam yetkili (yönetici) her basamakta karar verebilir: kişi ayrılsa da talep kilitli kalmaz.
    public static bool YetkiliMi(Guid? basamakRolu, Guid? basamakKisisi, Guid kullaniciId, Guid? kullaniciRolu, bool tamYetkiliMi) =>
        tamYetkiliMi || basamakKisisi == kullaniciId || (basamakRolu is not null && basamakRolu == kullaniciRolu);
}
