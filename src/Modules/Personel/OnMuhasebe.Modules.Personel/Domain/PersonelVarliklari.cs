using OnMuhasebe.Modules.Personel.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Dogrulama;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Personel.Domain;

internal static class PersonelHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("personel_bulunamadi", "Personel bulunamadı.");
    public static readonly Hata TcGecersiz = Hata.Dogrulama("tc_gecersiz", "TC kimlik numarası geçersiz.");
    public static readonly Hata TcKullaniliyor = Hata.Cakisma("tc_kullaniliyor", "Bu TC kimlik numarası başka bir personelde kayıtlı.");
    public static readonly Hata IbanGecersiz = Hata.Dogrulama("iban_gecersiz", "IBAN geçersiz.");
    public static readonly Hata KullaniciGecersiz = Hata.Dogrulama("kullanici_gecersiz", "Seçilen kullanıcı bu firmada bulunamadı.");
    public static readonly Hata KullaniciKullaniliyor = Hata.Cakisma("kullanici_kullaniliyor", "Bu kullanıcı başka bir personele bağlı.");
    public static readonly Hata MasrafMerkezi = Hata.Dogrulama("masraf_merkezi", "Masraf merkezi bulunamadı ya da pasif.");
    public static readonly Hata HareketliSilinemez = Hata.IsKurali("personel_hareketli", "Maaş, avans, masraf ya da zimmet kaydı olan personel silinemez; işten çıkış girin.");
    public static readonly Hata CikisTarihi = Hata.Dogrulama("cikis_tarihi", "İşten çıkış tarihi işe girişten önce olamaz.");
    public static readonly Hata Cikti = Hata.IsKurali("personel_cikti", "İşten çıkmış personele bu işlem yapılamaz.");

    public static readonly Hata DonemBulunamadi = Hata.Bulunamadi("donem_bulunamadi", "Maaş dönemi bulunamadı.");
    public static readonly Hata DonemVar = Hata.Cakisma("donem_var", "Bu ayın maaş dönemi zaten açık.");
    public static readonly Hata DonemTaslakDegil = Hata.IsKurali("donem_taslak_degil", "Onaylanmış dönem değiştirilemez; önce onayı geri alın.");
    public static readonly Hata DonemOnayliDegil = Hata.IsKurali("donem_onayli_degil", "Ödeme için dönem onaylanmalıdır.");
    public static readonly Hata DonemOdemeli = Hata.IsKurali("donem_odemeli", "Ödemesi olan dönemin onayı geri alınamaz; önce ödemeleri iptal edin.");
    public static readonly Hata PersonelYok = Hata.IsKurali("donem_personel_yok", "Bu ay çalışan personel bulunamadı.");
    public static readonly Hata SatirBulunamadi = Hata.Bulunamadi("satir_bulunamadi", "Maaş satırı bulunamadı.");
    public static readonly Hata EksiOdenecek = Hata.Dogrulama("eksi_odenecek", "Kesinti ve avans düşüldükten sonra ödenecek tutar eksi olamaz.");
    public static readonly Hata AvansFazla = Hata.Dogrulama("avans_fazla", "Avans kesintisi personelin açık avansını aşamaz.");
    public static readonly Hata TutarFazla = Hata.Dogrulama("tutar_fazla", "Ödeme tutarı kalan tutarı aşamaz.");
    public static readonly Hata OdenecekYok = Hata.IsKurali("odenecek_yok", "Ödenecek tutar yok.");
    public static readonly Hata TekSatir = Hata.Dogrulama("tek_satir", "Kısmi tutar yalnız tek kişi için girilebilir.");

    public static readonly Hata HesapGerekli = Hata.Dogrulama("hesap_gerekli", "Ödemenin yapılacağı kasa ya da banka hesabını seçin.");
    public static readonly Hata HesapTuru = Hata.IsKurali("hesap_uygun_degil", "Personel ödemesi yalnız aktif TL kasa ya da banka hesabından yapılır.");
    public static readonly Hata OdemeBulunamadi = Hata.Bulunamadi("odeme_bulunamadi", "Ödeme bulunamadı.");
    public static readonly Hata OdemeIptal = Hata.IsKurali("odeme_iptal", "Ödeme zaten iptal edilmiş.");

    public static readonly Hata AvansBulunamadi = Hata.Bulunamadi("avans_bulunamadi", "Avans bulunamadı.");
    public static readonly Hata AvansMahsuplu = Hata.IsKurali("avans_mahsuplu", "Maaştan düşülmeye başlanan avans iptal edilemez.");

    public static readonly Hata MasrafBulunamadi = Hata.Bulunamadi("masraf_bulunamadi", "Masraf beyanı bulunamadı.");
    public static readonly Hata MasrafDegismez = Hata.IsKurali("masraf_degismez", "Yalnız onay bekleyen ya da reddedilen masraf değiştirilebilir.");
    public static readonly Hata MasrafOnayda = Hata.IsKurali("masraf_onayda_degil", "Masraf onay beklemiyor.");
    public static readonly Hata KendiMasrafi = Hata.IsKurali("kendi_masrafi", "Kendi masrafınızı onaylayamaz ya da reddedemezsiniz.");
    public static readonly Hata RedGerekcesi = Hata.Dogrulama("red_gerekcesi", "Reddetmek için gerekçe yazın.");
    public static readonly Hata MasrafOdenmez = Hata.IsKurali("masraf_odenmez", "Yalnız onaylanmış masraf ödenir.");
    public static readonly Hata BagliPersonelYok = Hata.IsKurali("bagli_personel_yok", "Kullanıcınız bir personel kartına bağlı değil.");

    public static readonly Hata ZimmetBulunamadi = Hata.Bulunamadi("zimmet_bulunamadi", "Zimmet bulunamadı.");
    public static readonly Hata ZimmetKapali = Hata.IsKurali("zimmet_kapali", "Zimmet zaten kapatılmış.");
    public static readonly Hata IadeTarihi = Hata.Dogrulama("iade_tarihi", "İade tarihi veriliş tarihinden önce olamaz.");
}

internal static class MaasHesabi
{
    public static decimal Odenecek(decimal net, decimal prim, decimal kesinti, decimal avans) => net + prim - kesinti - avans;

    // Taksitli avansın bu ay düşülecek tutarı: eşit taksit, son taksit kalanı kapatır, maaşı aşmaz.
    public static decimal Taksit(decimal tutar, int taksitSayisi, decimal kalan) =>
        Math.Min(kalan, Para.Yuvarla(tutar / Math.Max(taksitSayisi, 1)));

    // Avans kesintisi en eski avanstan başlayarak dağıtılır.
    public static IReadOnlyList<(Guid AvansId, decimal Tutar)> Dagit(decimal kesinti, IEnumerable<(Guid AvansId, decimal Kalan)> avanslar)
    {
        var sonuc = new List<(Guid, decimal)>();
        foreach (var (id, kalan) in avanslar)
        {
            if (kesinti <= 0)
            {
                break;
            }

            var pay = Math.Min(kesinti, kalan);
            if (pay > 0)
            {
                sonuc.Add((id, pay));
                kesinti -= pay;
            }
        }

        return sonuc;
    }

    public static DateOnly DonemSonu(int yil, int ay) => new(yil, ay, DateTime.DaysInMonth(yil, ay));

    // Dönemde çalışan: dönem sonuna kadar girmiş, dönem başından önce çıkmamış.
    public static bool DonemdeCalisiyorMu(DateOnly giris, DateOnly? cikis, int yil, int ay) =>
        giris <= DonemSonu(yil, ay) && (cikis is null || cikis >= new DateOnly(yil, ay, 1));

    public static string? Maskele(string? deger, int bas = 3, int son = 2) =>
        deger is null ? null : deger.Length <= bas + son ? new string('*', deger.Length) : $"{deger[..bas]}{new string('*', deger.Length - bas - son)}{deger[^son..]}";
}

internal sealed class PersonelKarti : FirmaEntity, ISilinebilir, IIsyeriVarligi
{
    private PersonelKarti() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public string Soyad { get; private set; } = null!;
    public string? TcKimlik { get; private set; }
    public DateOnly IseGiris { get; private set; }
    public DateOnly? IstenCikis { get; private set; }
    public string? Gorev { get; private set; }
    public string? Departman { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public Guid? MasrafMerkeziId { get; private set; }
    public string? Iban { get; private set; }
    public decimal NetMaas { get; private set; }
    public string? Telefon { get; private set; }
    public string? Eposta { get; private set; }
    public Guid? KullaniciId { get; private set; }
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public string AdSoyad => $"{Ad} {Soyad}";

    public static Sonuc<PersonelKarti> Olustur(string kod, PersonelKaydetIstegi i, Guid? isyeriId)
    {
        var p = new PersonelKarti { Kod = kod };
        var sonuc = p.Ayarla(i, isyeriId, true);
        return sonuc.BasariliMi ? p : sonuc.Hata;
    }

    public Sonuc Guncelle(PersonelKaydetIstegi i, Guid? isyeriId, bool maasYetkisi) => Ayarla(i, isyeriId, maasYetkisi);

    private Sonuc Ayarla(PersonelKaydetIstegi i, Guid? isyeriId, bool maasYetkisi)
    {
        if (maasYetkisi)
        {
            var tc = string.IsNullOrWhiteSpace(i.TcKimlik) ? null : i.TcKimlik.Trim();
            if (tc is not null && !VergiKimlik.TcknGecerliMi(tc))
            {
                return PersonelHatalari.TcGecersiz;
            }

            var iban = string.IsNullOrWhiteSpace(i.Iban) ? null : OnMuhasebe.Shared.Domain.Dogrulama.Iban.Normalize(i.Iban);
            if (iban is not null && !OnMuhasebe.Shared.Domain.Dogrulama.Iban.GecerliMi(iban))
            {
                return PersonelHatalari.IbanGecersiz;
            }

            (TcKimlik, Iban, NetMaas) = (tc, iban, i.NetMaas ?? NetMaas);
        }

        Ad = i.Ad.Trim();
        Soyad = i.Soyad.Trim();
        IseGiris = i.IseGiris;
        Gorev = Temiz(i.Gorev);
        Departman = Temiz(i.Departman);
        IsyeriId = isyeriId;
        MasrafMerkeziId = i.MasrafMerkeziId;
        Telefon = Temiz(i.Telefon);
        Eposta = Temiz(i.Eposta);
        KullaniciId = i.KullaniciId;
        return Sonuc.Basarili();
    }

    public Sonuc Cikis(DateOnly tarih)
    {
        if (tarih < IseGiris)
        {
            return PersonelHatalari.CikisTarihi;
        }

        IstenCikis = tarih;
        return Sonuc.Basarili();
    }

    private static string? Temiz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

internal sealed class MaasDonemi : FirmaEntity
{
    private readonly List<MaasSatiri> _satirlar = [];

    private MaasDonemi() { }

    public int Yil { get; private set; }
    public int Ay { get; private set; }
    public MaasDonemiDurumu Durum { get; private set; }

    public IReadOnlyList<MaasSatiri> Satirlar => _satirlar;

    public static MaasDonemi Olustur(int yil, int ay) => new() { Yil = yil, Ay = ay, Durum = MaasDonemiDurumu.Taslak };

    public void SatirEkle(MaasSatiri s) => _satirlar.Add(s);

    public Sonuc Onayla()
    {
        if (Durum != MaasDonemiDurumu.Taslak)
        {
            return PersonelHatalari.DonemTaslakDegil;
        }

        Durum = MaasDonemiDurumu.Onayli;
        return Sonuc.Basarili();
    }

    public Sonuc GeriAl()
    {
        if (Durum != MaasDonemiDurumu.Onayli)
        {
            return PersonelHatalari.DonemOnayliDegil;
        }

        if (_satirlar.Any(s => s.Odenen > 0))
        {
            return PersonelHatalari.DonemOdemeli;
        }

        Durum = MaasDonemiDurumu.Taslak;
        return Sonuc.Basarili();
    }
}

internal sealed class MaasSatiri : FirmaEntity, IIsyeriVarligi
{
    private MaasSatiri() { }

    public Guid DonemId { get; private set; }
    public Guid PersonelId { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public decimal NetMaas { get; private set; }
    public decimal Prim { get; private set; }
    public decimal Kesinti { get; private set; }
    public decimal AvansKesintisi { get; private set; }
    public decimal Odenen { get; private set; }
    public string? Not { get; private set; }

    public decimal Odenecek => MaasHesabi.Odenecek(NetMaas, Prim, Kesinti, AvansKesintisi);

    public decimal Kalan => Odenecek - Odenen;

    public static MaasSatiri Olustur(Guid donemId, Guid personelId, Guid? isyeriId, decimal net, decimal avans) =>
        new() { DonemId = donemId, PersonelId = personelId, IsyeriId = isyeriId, NetMaas = net, AvansKesintisi = Math.Min(avans, net) };

    public Sonuc Guncelle(MaasSatirIstegi i)
    {
        if (MaasHesabi.Odenecek(i.NetMaas, i.Prim, i.Kesinti, i.AvansKesintisi) < 0)
        {
            return PersonelHatalari.EksiOdenecek;
        }

        (NetMaas, Prim, Kesinti, AvansKesintisi, Not) = (i.NetMaas, i.Prim, i.Kesinti, i.AvansKesintisi, string.IsNullOrWhiteSpace(i.Not) ? null : i.Not.Trim());
        return Sonuc.Basarili();
    }

    public Sonuc OdemeEkle(decimal tutar)
    {
        if (tutar <= 0 || tutar > Kalan)
        {
            return PersonelHatalari.TutarFazla;
        }

        Odenen += tutar;
        return Sonuc.Basarili();
    }

    public void OdemeGeriAl(decimal tutar) => Odenen -= tutar;
}

internal sealed class Avans : FirmaEntity, IDonemeTabi
{
    private Avans() { }

    public Guid PersonelId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public decimal Tutar { get; private set; }
    public int TaksitSayisi { get; private set; }
    [DonemKilidiDisinda] public decimal Kalan { get; private set; }
    [DonemKilidiDisinda] public bool IptalMi { get; private set; }
    public string? Aciklama { get; private set; }

    public static Avans Olustur(AvansKaydetIstegi i) => new()
    {
        PersonelId = i.PersonelId, Tarih = i.Tarih, Tutar = i.Tutar, TaksitSayisi = i.TaksitSayisi, Kalan = i.Tutar, Aciklama = string.IsNullOrWhiteSpace(i.Aciklama) ? null : i.Aciklama.Trim()
    };

    public void Dus(decimal tutar) => Kalan -= tutar;

    public void GeriEkle(decimal tutar) => Kalan += tutar;

    public Sonuc IptalEt()
    {
        if (Kalan != Tutar)
        {
            return PersonelHatalari.AvansMahsuplu;
        }

        if (IptalMi)
        {
            return PersonelHatalari.OdemeIptal;
        }

        (IptalMi, Kalan) = (true, 0);
        return Sonuc.Basarili();
    }
}

// Dönem onaylanınca avansın maaştan düşülen kısmı. Onay geri alınırsa silinir ve avansın kalanı döner.
internal sealed class AvansMahsubu : FirmaEntity, IDonemeTabi
{
    private AvansMahsubu() { }

    public Guid AvansId { get; private set; }
    public Guid SatirId { get; private set; }
    public Guid PersonelId { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public decimal Tutar { get; private set; }

    public static AvansMahsubu Olustur(Guid avansId, MaasSatiri satir, DateOnly tarih, decimal tutar) =>
        new() { AvansId = avansId, SatirId = satir.Id, PersonelId = satir.PersonelId, IsyeriId = satir.IsyeriId, Tarih = tarih, Tutar = tutar };
}

internal sealed class PersonelOdemesi : FirmaEntity, IDonemeTabi
{
    private PersonelOdemesi() { }

    public Guid PersonelId { get; private set; }
    public PersonelOdemeTuru Tur { get; private set; }
    public Guid KaynakId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public decimal Tutar { get; private set; }
    public Guid? HesapId { get; private set; }
    [DonemKilidiDisinda] public bool IptalMi { get; private set; }

    public static PersonelOdemesi Olustur(Guid personelId, PersonelOdemeTuru tur, Guid kaynakId, DateOnly tarih, decimal tutar, Guid? hesapId) =>
        new() { PersonelId = personelId, Tur = tur, KaynakId = kaynakId, Tarih = tarih, Tutar = tutar, HesapId = hesapId };

    public Sonuc IptalEt()
    {
        if (IptalMi)
        {
            return PersonelHatalari.OdemeIptal;
        }

        IptalMi = true;
        return Sonuc.Basarili();
    }
}

internal sealed class MasrafBeyani : FirmaEntity, ISilinebilir, IDonemeTabi, IIsyeriVarligi
{
    private MasrafBeyani() { }

    public Guid PersonelId { get; private set; }
    public Guid? BeyanEdenKullaniciId { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public MasrafKategorisi Kategori { get; private set; }
    public decimal Tutar { get; private set; }
    public string Aciklama { get; private set; } = null!;
    public Guid? MasrafMerkeziId { get; private set; }
    [DonemKilidiDisinda] public MasrafDurumu Durum { get; private set; }
    [DonemKilidiDisinda] public Guid? KararVerenId { get; private set; }
    [DonemKilidiDisinda] public string? KararVeren { get; private set; }
    [DonemKilidiDisinda] public DateTime? KararTarihi { get; private set; }
    [DonemKilidiDisinda] public string? Not { get; private set; }
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public static MasrafBeyani Olustur(Guid personelId, Guid? personelKullanicisi, Guid? isyeriId, MasrafKaydetIstegi i)
    {
        var m = new MasrafBeyani { PersonelId = personelId, BeyanEdenKullaniciId = personelKullanicisi, IsyeriId = isyeriId, Durum = MasrafDurumu.Beyan };
        m.Ayarla(i);
        return m;
    }

    public Sonuc Guncelle(MasrafKaydetIstegi i)
    {
        if (Durum is not (MasrafDurumu.Beyan or MasrafDurumu.Reddedildi))
        {
            return PersonelHatalari.MasrafDegismez;
        }

        Ayarla(i);
        (Durum, KararVerenId, KararVeren, KararTarihi, Not) = (MasrafDurumu.Beyan, null, null, null, null);
        return Sonuc.Basarili();
    }

    private void Ayarla(MasrafKaydetIstegi i) =>
        (Tarih, Kategori, Tutar, Aciklama, MasrafMerkeziId) = (i.Tarih, i.Kategori, i.Tutar, i.Aciklama.Trim(), i.MasrafMerkeziId);

    // Masraf, personele bağlı kullanıcıya aitse o kişi karar veremez; yönetici olsa da.
    public Sonuc Karar(Guid kullaniciId, string ad, Guid? personelKullanicisi, bool onayMi, string? not, DateTime simdi)
    {
        if (Durum != MasrafDurumu.Beyan)
        {
            return PersonelHatalari.MasrafOnayda;
        }

        if (kullaniciId == personelKullanicisi || kullaniciId == BeyanEdenKullaniciId)
        {
            return PersonelHatalari.KendiMasrafi;
        }

        if (!onayMi && string.IsNullOrWhiteSpace(not))
        {
            return PersonelHatalari.RedGerekcesi;
        }

        (Durum, KararVerenId, KararVeren, KararTarihi, Not) = (onayMi ? MasrafDurumu.Onaylandi : MasrafDurumu.Reddedildi, kullaniciId, ad, simdi, string.IsNullOrWhiteSpace(not) ? null : not.Trim());
        return Sonuc.Basarili();
    }

    public Sonuc Odendi()
    {
        if (Durum != MasrafDurumu.Onaylandi)
        {
            return PersonelHatalari.MasrafOdenmez;
        }

        Durum = MasrafDurumu.Odendi;
        return Sonuc.Basarili();
    }

    public void OdemeGeriAl() => Durum = MasrafDurumu.Onaylandi;
}

internal sealed class Zimmet : FirmaEntity, ISilinebilir
{
    private Zimmet() { }

    public Guid PersonelId { get; private set; }
    public string Ad { get; private set; } = null!;
    public string? SeriNo { get; private set; }
    public DateOnly VerilisTarihi { get; private set; }
    public DateOnly? IadeTarihi { get; private set; }
    public decimal? Bedel { get; private set; }
    public ZimmetDurumu Durum { get; private set; }
    public string? Not { get; private set; }
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public static Zimmet Olustur(ZimmetKaydetIstegi i)
    {
        var z = new Zimmet { Durum = ZimmetDurumu.Zimmette };
        z.Ayarla(i);
        return z;
    }

    public void Ayarla(ZimmetKaydetIstegi i) =>
        (PersonelId, Ad, SeriNo, VerilisTarihi, Bedel, Not) = (i.PersonelId, i.Ad.Trim(), string.IsNullOrWhiteSpace(i.SeriNo) ? null : i.SeriNo.Trim(), i.VerilisTarihi, i.Bedel, string.IsNullOrWhiteSpace(i.Not) ? null : i.Not.Trim());

    public Sonuc Kapat(ZimmetIadeIstegi i)
    {
        if (Durum != ZimmetDurumu.Zimmette)
        {
            return PersonelHatalari.ZimmetKapali;
        }

        if (i.Tarih < VerilisTarihi)
        {
            return PersonelHatalari.IadeTarihi;
        }

        (Durum, IadeTarihi) = (i.KayipMi ? ZimmetDurumu.Kayip : ZimmetDurumu.IadeEdildi, i.Tarih);
        if (!string.IsNullOrWhiteSpace(i.Not))
        {
            Not = i.Not.Trim();
        }

        return Sonuc.Basarili();
    }
}
