using OnMuhasebe.Modules.Bakim.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Bakim.Domain;

internal static class BakimHatalari
{
    public static readonly Hata EkipmanYok = Hata.Bulunamadi("ekipman_bulunamadi", "Ekipman bulunamadı.");
    public static readonly Hata PlanYok = Hata.Bulunamadi("plan_bulunamadi", "Bakım planı bulunamadı.");
    public static readonly Hata IsEmriYok = Hata.Bulunamadi("is_emri_bulunamadi", "İş emri bulunamadı.");
    public static readonly Hata KodKullaniliyor = Hata.Cakisma("kod_kullaniliyor", "Bu ekipman kodu kullanılıyor.");
    public static readonly Hata PeriyotGerekli = Hata.Dogrulama("periyot_gerekli", "Gün ya da sayaç periyodu girilmelidir.");
    public static readonly Hata SayacBirimiYok = Hata.Dogrulama("sayac_birimi", "Sayaç periyodu için ekipmanda sayaç birimi seçilmelidir.");
    public static readonly Hata PlanEkipmanFarkli = Hata.Dogrulama("plan_ekipman", "Seçilen plan bu ekipmana ait değil.");
    public static readonly Hata SorumluGecersiz = Hata.Dogrulama("sorumlu_gecersiz", "Sorumlu kişi bu firmanın aktif kullanıcısı olmalıdır.");
    public static readonly Hata MasrafMerkeziGecersiz = Hata.Dogrulama("masraf_merkezi", "Masraf merkezi bulunamadı ya da pasif.");
    public static readonly Hata DemirbasGecersiz = Hata.Dogrulama("demirbas_gecersiz", "Demirbaş kartı bulunamadı.");
    public static readonly Hata FaturaGecersiz = Hata.Dogrulama("fatura_gecersiz", "Bağlanan fatura bulunamadı.");
    public static readonly Hata Degistirilemez = Hata.IsKurali("is_emri_kapali", "Kapanmış ya da iptal edilmiş iş emri değiştirilemez.");
    public static readonly Hata GecersizGecis = Hata.IsKurali("is_emri_durumu", "İş emri bu durumda bu işleme uygun değil.");
    public static readonly Hata BitisBaslangictanOnce = Hata.Dogrulama("bitis_zamani", "Bitiş zamanı başlangıçtan önce olamaz.");
    public static readonly Hata ParcaAdiGerekli = Hata.Dogrulama("parca_adi", "Stok kartı seçilmeyen parçada ad gerekli.");
    public static readonly Hata StokKartiGecersiz = Hata.Dogrulama("stok_karti", "Parçanın stok kartı bulunamadı, pasif ya da hizmet kartı.");
    public static readonly Hata StokBirimGecersiz = Hata.Dogrulama("stok_birim", "Parçanın birimi stok kartında tanımlı değil.");
    public static readonly Hata StokIzlemeli = Hata.IsKurali("stok_izlemeli", "Lot ya da seri takipli parça iş emrine eklenemez; stok ekranından çıkış yapıp burada elle yazın.");
    public static readonly Hata DepoGecersiz = Hata.Dogrulama("depo_gecersiz", "Parçanın deposu bulunamadı ya da pasif.");
    public static readonly Hata StokLisansiYok = Hata.IsKurali("stok_lisansi", "Stok kartı seçmek için Stok modülü gerekli; parçayı elle yazabilirsiniz.");
}

internal sealed class Ekipman : FirmaEntity, ISilinebilir, IIsyeriVarligi
{
    private Ekipman() { }

    public string Kod { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public EkipmanTuru Tur { get; private set; }
    public string? Marka { get; private set; }
    public string? Model { get; private set; }
    public string? SeriNo { get; private set; }
    public int? UretimYili { get; private set; }
    public string? Konum { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public Guid? SorumluKullaniciId { get; private set; }
    public Guid? DemirbasId { get; private set; }
    public Guid? MasrafMerkeziId { get; private set; }
    public SayacBirimi SayacBirimi { get; private set; }
    public decimal? Sayac { get; private set; }
    public EkipmanDurumu Durum { get; private set; }
    public string? Aciklama { get; private set; }
    public bool SilindiMi { get; private set; }
    public DateTime? SilinmeTarihi { get; private set; }

    public static Ekipman Olustur(string kod, EkipmanKaydetIstegi i)
    {
        var e = new Ekipman();
        e.Guncelle(kod, i);
        return e;
    }

    public void Guncelle(string kod, EkipmanKaydetIstegi i)
    {
        Kod = kod;
        Ad = i.Ad.Trim();
        Tur = i.Tur;
        Marka = BakimMetni.Temiz(i.Marka);
        Model = BakimMetni.Temiz(i.Model);
        SeriNo = BakimMetni.Temiz(i.SeriNo);
        UretimYili = i.UretimYili;
        Konum = BakimMetni.Temiz(i.Konum);
        IsyeriId = i.IsyeriId;
        SorumluKullaniciId = i.SorumluKullaniciId;
        DemirbasId = i.DemirbasId;
        MasrafMerkeziId = i.MasrafMerkeziId;
        SayacBirimi = i.SayacBirimi;
        Sayac = i.SayacBirimi == SayacBirimi.Yok ? null : i.Sayac;
        Durum = i.Durum;
        Aciklama = BakimMetni.Temiz(i.Aciklama);
    }

    public void SayacIlerlet(decimal? deger)
    {
        if (deger is { } d && SayacBirimi != SayacBirimi.Yok && d > (Sayac ?? 0))
        {
            Sayac = d;
        }
    }

    // Hizmet dışı ekipmanın durumunu iş emri değiştirmez.
    public void DurumAta(EkipmanDurumu durum)
    {
        if (Durum != EkipmanDurumu.HizmetDisi)
        {
            Durum = durum;
        }
    }
}

internal sealed class BakimPlani : FirmaEntity
{
    private BakimPlani() { }

    public Guid EkipmanId { get; private set; }
    public string Ad { get; private set; } = null!;
    public bool YasalMi { get; private set; }
    public string? KontrolKurulusu { get; private set; }
    public int? PeriyotGun { get; private set; }
    public decimal? PeriyotSayac { get; private set; }
    public DateOnly? SonYapilmaTarihi { get; private set; }
    public decimal? SonYapilmaSayaci { get; private set; }
    public DateOnly? SonrakiTarih { get; private set; }
    public decimal? SonrakiSayac { get; private set; }
    public int HatirlatmaGun { get; private set; }
    public bool AktifMi { get; private set; }

    public static Sonuc<BakimPlani> Olustur(Guid ekipmanId, BakimPlaniKaydetIstegi i, DateOnly bugun, decimal? sayac)
    {
        var plan = new BakimPlani { EkipmanId = ekipmanId };
        var sonuc = plan.Guncelle(i, bugun, sayac);
        return sonuc.BasariliMi ? plan : sonuc.Hata;
    }

    public Sonuc Guncelle(BakimPlaniKaydetIstegi i, DateOnly bugun, decimal? sayac)
    {
        if (i.PeriyotGun is null && i.PeriyotSayac is null)
        {
            return BakimHatalari.PeriyotGerekli;
        }

        Ad = i.Ad.Trim();
        YasalMi = i.YasalMi;
        KontrolKurulusu = BakimMetni.Temiz(i.KontrolKurulusu);
        PeriyotGun = i.PeriyotGun;
        PeriyotSayac = i.PeriyotSayac;
        HatirlatmaGun = i.HatirlatmaGun;
        AktifMi = i.AktifMi;
        SonrakiTarih = i.PeriyotGun is { } gun ? i.SonrakiTarih ?? SonrakiTarih ?? bugun.AddDays(gun) : null;
        SonrakiSayac = i.PeriyotSayac is { } periyot ? i.SonrakiSayac ?? SonrakiSayac ?? (sayac ?? 0) + periyot : null;
        return Sonuc.Basarili();
    }

    // Sonraki bakım yapıldığı günden ve yapıldığı sayaçtan sayılır.
    public void Yapildi(DateOnly tarih, decimal? sayac)
    {
        SonYapilmaTarihi = tarih;
        SonYapilmaSayaci = sayac;
        if (PeriyotGun is { } gun)
        {
            SonrakiTarih = tarih.AddDays(gun);
        }

        if (PeriyotSayac is { } periyot)
        {
            SonrakiSayac = (sayac ?? SonrakiSayac ?? 0) + periyot;
        }
    }
}

internal sealed class IsEmri : FirmaEntity
{
    private readonly List<IsEmriParcasi> _parcalar = [];

    private IsEmri() { }

    public string Kod { get; private set; } = null!;
    public Guid EkipmanId { get; private set; }
    public IsEmriTuru Tur { get; private set; }
    public Guid? PlanId { get; private set; }
    public IsEmriDurumu Durum { get; private set; }
    public string Baslik { get; private set; } = null!;
    public string? Aciklama { get; private set; }
    public DateTime BildirimZamani { get; private set; }
    public DateTime? BaslangicZamani { get; private set; }
    public DateTime? BitisZamani { get; private set; }
    public bool MakineDurduMu { get; private set; }
    public decimal IscilikTutari { get; private set; }
    public decimal DisServisTutari { get; private set; }
    public string? DisServisFirma { get; private set; }
    public Guid? FaturaId { get; private set; }
    public string? KontrolKurulusu { get; private set; }
    public string? RaporNo { get; private set; }
    public decimal? SayacDegeri { get; private set; }

    public IReadOnlyList<IsEmriParcasi> Parcalar => _parcalar;

    public bool AcikMi => Durum is IsEmriDurumu.Acik or IsEmriDurumu.Devam;

    public decimal? DurusSaati => BakimHesaplari.DurusSaati(MakineDurduMu, Tur, BildirimZamani, BaslangicZamani, BitisZamani);

    public static IsEmri Olustur(string kod, IsEmriKaydetIstegi i, DateTime simdi)
    {
        var e = new IsEmri { Kod = kod, EkipmanId = i.EkipmanId, Durum = IsEmriDurumu.Acik };
        e.Ayarla(i, simdi);
        return e;
    }

    public Sonuc Guncelle(IsEmriKaydetIstegi i, DateTime simdi)
    {
        if (!AcikMi)
        {
            return BakimHatalari.Degistirilemez;
        }

        Ayarla(i, simdi);
        return Sonuc.Basarili();
    }

    private void Ayarla(IsEmriKaydetIstegi i, DateTime simdi)
    {
        Tur = i.Tur;
        PlanId = i.PlanId;
        Baslik = i.Baslik.Trim();
        Aciklama = BakimMetni.Temiz(i.Aciklama);
        BildirimZamani = BakimHesaplari.Utc(i.BildirimZamani ?? simdi);
        MakineDurduMu = i.MakineDurduMu;
        IscilikTutari = Para.Yuvarla(i.IscilikTutari);
        DisServisTutari = Para.Yuvarla(i.DisServisTutari);
        DisServisFirma = BakimMetni.Temiz(i.DisServisFirma);
        FaturaId = i.FaturaId;
        KontrolKurulusu = BakimMetni.Temiz(i.KontrolKurulusu);
        RaporNo = BakimMetni.Temiz(i.RaporNo);
        _parcalar.Clear();
        foreach (var p in i.Parcalar ?? [])
        {
            _parcalar.Add(IsEmriParcasi.Olustur(Id, p));
        }
    }

    public Sonuc Baslat(DateTime simdi)
    {
        if (Durum != IsEmriDurumu.Acik)
        {
            return BakimHatalari.GecersizGecis;
        }

        Durum = IsEmriDurumu.Devam;
        BaslangicZamani = simdi;
        return Sonuc.Basarili();
    }

    public Sonuc Kapat(IsEmriKapatmaIstegi i)
    {
        if (!AcikMi)
        {
            return BakimHatalari.GecersizGecis;
        }

        // Bitiş ekranda dakika hassasiyetinde girilir: hemen kapatılan iş saniyeli başlangıçtan biraz önce görünebilir.
        var enErken = BaslangicZamani is { } baslangic && baslangic > BildirimZamani ? baslangic : BildirimZamani;
        var bitis = BakimHesaplari.Utc(i.BitisZamani);
        if (bitis < enErken.AddMinutes(-1))
        {
            return BakimHatalari.BitisBaslangictanOnce;
        }

        Durum = IsEmriDurumu.Kapandi;
        BaslangicZamani ??= BildirimZamani;
        BitisZamani = bitis < enErken ? enErken : bitis;
        SayacDegeri = i.SayacDegeri;
        RaporNo = BakimMetni.Temiz(i.RaporNo) ?? RaporNo;
        return Sonuc.Basarili();
    }

    public Sonuc IptalEt()
    {
        if (Durum == IsEmriDurumu.Iptal)
        {
            return BakimHatalari.GecersizGecis;
        }

        Durum = IsEmriDurumu.Iptal;
        return Sonuc.Basarili();
    }
}

internal sealed class IsEmriParcasi : FirmaEntity
{
    private IsEmriParcasi() { }

    public Guid IsEmriId { get; private set; }
    public Guid? StokKartiId { get; private set; }
    public Guid? DepoId { get; private set; }
    public string Ad { get; private set; } = null!;
    public string? Birim { get; private set; }
    public decimal Miktar { get; private set; }
    public decimal? BirimFiyat { get; private set; }

    public static IsEmriParcasi Olustur(Guid isEmriId, IsEmriParcaIstegi i) => new()
    {
        IsEmriId = isEmriId,
        StokKartiId = i.StokKartiId,
        DepoId = i.DepoId,
        Ad = (i.Ad ?? "").Trim(),
        Birim = BakimMetni.Temiz(i.Birim),
        Miktar = i.Miktar,
        BirimFiyat = i.StokKartiId is null ? i.BirimFiyat : null
    };
}

internal static class BakimMetni
{
    public static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal static class BakimHesaplari
{
    // Sayaçta "yaklaşıyor" eşiği: periyodun onda biri kala.
    private const decimal SayacEsikOrani = 0.10m;

    public static DateTime Utc(DateTime zaman) => zaman.Kind == DateTimeKind.Utc ? zaman : zaman.ToUniversalTime();

    // Gün ya da sayaçtan hangisi daha kötüyse o geçerlidir.
    public static PlanDurumu Durum(DateOnly? sonrakiTarih, int hatirlatmaGun, decimal? sonrakiSayac, decimal? periyotSayac, decimal? sayac, DateOnly bugun)
    {
        var durum = PlanDurumu.Zamaninda;
        if (sonrakiTarih is { } tarih)
        {
            durum = tarih < bugun ? PlanDurumu.Gecikti : tarih.DayNumber - bugun.DayNumber <= hatirlatmaGun ? PlanDurumu.Yaklasiyor : durum;
        }

        if (sonrakiSayac is { } hedef && sayac is { } guncel)
        {
            var sayacDurumu = guncel >= hedef ? PlanDurumu.Gecikti
                : hedef - guncel <= (periyotSayac ?? 0) * SayacEsikOrani ? PlanDurumu.Yaklasiyor
                : PlanDurumu.Zamaninda;
            durum = sayacDurumu > durum ? sayacDurumu : durum;
        }

        return durum;
    }

    // Arızada duruş bildirimden, planlı işte işe başlamadan sayılır. Makine durmadıysa ya da iş bitmediyse duruş yoktur.
    public static decimal? DurusSaati(bool makineDurduMu, IsEmriTuru tur, DateTime bildirim, DateTime? baslangic, DateTime? bitis)
    {
        if (!makineDurduMu || bitis is not { } son)
        {
            return null;
        }

        var bas = tur == IsEmriTuru.Ariza ? bildirim : baslangic ?? bildirim;
        return son <= bas ? 0 : Math.Round((decimal)(son - bas).TotalHours, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ElleParcaTutari(IEnumerable<IsEmriParcasi> parcalar) =>
        parcalar.Where(p => p.StokKartiId is null).Sum(p => Para.Yuvarla(p.Miktar * (p.BirimFiyat ?? 0)));
}
