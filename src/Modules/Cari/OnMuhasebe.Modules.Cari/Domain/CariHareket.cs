using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Cari.Domain;

// Değiştirilemez muhasebe kaydı. Tutarlar carinin para birimindedir; TL karşılıkları işlem kuruyla sabitlenir.
// Düzeltme silme/güncelleme ile değil, ters kayıtla yapılır.
internal sealed class CariHareket : FirmaEntity, IDonemeTabi
{
    public const int KurOndalik = 6;

    private CariHareket() { }

    public Guid CariHesapId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public DateOnly? VadeTarihi { get; private set; }
    public CariHareketTuru Tur { get; private set; }
    public string? BelgeNo { get; private set; }
    public string? Aciklama { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public decimal Borc { get; private set; }
    public decimal Alacak { get; private set; }
    public decimal Kur { get; private set; }
    public decimal BorcTl { get; private set; }
    public decimal AlacakTl { get; private set; }

    // Fatura, tahsilat gibi başka modüldeki kaynak belge.
    public Guid? KaynakBelgeId { get; private set; }
    public Guid? IptalEdilenHareketId { get; private set; }
    [DonemKilidiDisinda]
    public Guid? IptalHareketId { get; private set; }

    // Elle kapama yapılan kalem otomatik (FIFO) kapamaya katılmaz; kullanıcının seçimi korunur.
    [DonemKilidiDisinda]
    public bool ElleKapamaMi { get; private set; }

    public bool IptalEdildiMi => IptalHareketId is not null;
    public bool IptalKaydiMi => IptalEdilenHareketId is not null;

    public void ElleKapamaAyarla(bool elle) => ElleKapamaMi = elle;

    // Kaynak belgeden gelen hareketler (fatura, tahsilat) yalnızca kendi belgesinden iptal edilir.
    public static bool ElleGirilebilirMi(CariHareketTuru tur) => tur is CariHareketTuru.Acilis or CariHareketTuru.Dekont;

    public static Sonuc<CariHareket> Olustur(
        CariHesap cari,
        CariHareketTuru tur,
        HareketYonu yon,
        DateOnly tarih,
        DateOnly? vadeTarihi,
        decimal tutar,
        decimal kur,
        string? belgeNo,
        string? aciklama,
        Guid? kaynakBelgeId = null)
    {
        if (!cari.AktifMi)
        {
            return CariHatalari.PasifCariyeHareket;
        }

        tutar = Para.Yuvarla(tutar);
        kur = Math.Round(kur, KurOndalik, MidpointRounding.AwayFromZero);

        if (tutar <= 0)
        {
            return CariHatalari.TutarPozitifOlmali;
        }

        if (kur <= 0)
        {
            return CariHatalari.KurPozitifOlmali;
        }

        if (cari.ParaBirimi == ParaBirimleri.Tl && kur != 1m)
        {
            return CariHatalari.TlKuruBirOlmali;
        }

        if (vadeTarihi < tarih)
        {
            return CariHatalari.VadeTarihtenOnceOlamaz;
        }

        var tl = Para.TlKarsiligi(tutar, kur);
        return new CariHareket
        {
            CariHesapId = cari.Id,
            Tarih = tarih,
            VadeTarihi = vadeTarihi,
            Tur = tur,
            BelgeNo = belgeNo,
            Aciklama = aciklama,
            ParaBirimi = cari.ParaBirimi,
            Borc = yon == HareketYonu.Borc ? tutar : 0m,
            Alacak = yon == HareketYonu.Alacak ? tutar : 0m,
            Kur = kur,
            BorcTl = yon == HareketYonu.Borc ? tl : 0m,
            AlacakTl = yon == HareketYonu.Alacak ? tl : 0m,
            KaynakBelgeId = kaynakBelgeId
        };
    }

    // Ters kayıt aynı tarih ve kurla atılır; böylece geçmiş tarihli bakiyeler de doğru kalır.
    public Sonuc<CariHareket> IptalEt()
    {
        if (IptalKaydiMi)
        {
            return CariHatalari.IptalKaydiIptalEdilemez;
        }

        if (IptalEdildiMi)
        {
            return CariHatalari.ZatenIptalEdilmis;
        }

        var ters = new CariHareket
        {
            CariHesapId = CariHesapId,
            Tarih = Tarih,
            VadeTarihi = VadeTarihi,
            Tur = Tur,
            BelgeNo = BelgeNo,
            Aciklama = $"İptal: {Aciklama}".TrimEnd(' ', ':'),
            ParaBirimi = ParaBirimi,
            Borc = Alacak,
            Alacak = Borc,
            Kur = Kur,
            BorcTl = AlacakTl,
            AlacakTl = BorcTl,
            KaynakBelgeId = KaynakBelgeId,
            IptalEdilenHareketId = Id
        };

        IptalHareketId = ters.Id;
        return ters;
    }
}
