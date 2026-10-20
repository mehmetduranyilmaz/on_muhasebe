using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Stok.Domain;

// Değiştirilemez stok kaydı. Miktarlar temel birimde; girilen birim ve miktar gösterim için ayrıca tutulur.
// BirimFiyat yalnızca girişlerde, temel birim başına TL maliyettir. Çıkış maliyeti rapor anında
// firmanın maliyet yöntemiyle hesaplanır (yöntem değişince geriye dönük tutarlı kalsın diye saklanmaz).
internal sealed class StokHareket : FirmaEntity, IDonemeTabi
{
    public const int MiktarOndalik = 4;

    private StokHareket() { }

    public Guid StokKartiId { get; private set; }
    public Guid DepoId { get; private set; }
    public DateOnly Tarih { get; private set; }
    public StokHareketTuru Tur { get; private set; }
    public decimal GirisMiktari { get; private set; }
    public decimal CikisMiktari { get; private set; }
    public string GirilenBirim { get; private set; } = null!;
    public decimal GirilenMiktar { get; private set; }
    public decimal? BirimFiyat { get; private set; }
    public string? BelgeNo { get; private set; }
    public string? Aciklama { get; private set; }
    public Guid? TransferId { get; private set; }
    public Guid? KaynakBelgeId { get; private set; }
    public Guid? IptalEdilenHareketId { get; private set; }
    [DonemKilidiDisinda]
    public Guid? IptalHareketId { get; private set; }

    public bool IptalEdildiMi => IptalHareketId is not null;
    public bool IptalKaydiMi => IptalEdilenHareketId is not null;

    public static bool GirisMi(StokHareketTuru tur) => tur is StokHareketTuru.Acilis or StokHareketTuru.SayimFazlasi
        or StokHareketTuru.TransferGiris or StokHareketTuru.AlisFaturasi or StokHareketTuru.SatistanIade;

    public static bool ElleGirilebilirMi(StokHareketTuru tur) => tur is StokHareketTuru.Acilis or StokHareketTuru.SayimFazlasi
        or StokHareketTuru.SayimEksigi or StokHareketTuru.Fire or StokHareketTuru.Sarf;

    public static bool TransferMi(StokHareketTuru tur) => tur is StokHareketTuru.TransferGiris or StokHareketTuru.TransferCikis;

    // Transferin maliyeti kaynak depodaki maliyettir; giriş bacağı fiyat istemez.
    private static bool MaliyetGerekliMi(StokHareketTuru tur) => GirisMi(tur) && !TransferMi(tur);

    public static Sonuc<StokHareket> Olustur(
        StokKarti kart,
        Guid depoId,
        StokHareketTuru tur,
        DateOnly tarih,
        string girilenBirim,
        decimal girilenMiktar,
        decimal? girilenBirimFiyat,
        string? belgeNo,
        string? aciklama,
        Guid? transferId = null,
        Guid? kaynakBelgeId = null)
    {
        if (kart.Tur == StokTuru.Hizmet)
        {
            return StokHatalari.HizmetKartinaHareket;
        }

        if (!kart.AktifMi)
        {
            return StokHatalari.PasifKartaHareket;
        }

        if (kart.Katsayi(girilenBirim) is not { } katsayi)
        {
            return StokHatalari.BirimTanimsiz;
        }

        if (girilenMiktar <= 0)
        {
            return StokHatalari.MiktarPozitifOlmali;
        }

        if (Shared.Contracts.Birimler.Birimler.Tumu.FirstOrDefault(b => b.Kod == girilenBirim) is { KesirliMi: false }
            && girilenMiktar != decimal.Truncate(girilenMiktar))
        {
            return StokHatalari.KesirliMiktar;
        }

        decimal? birimFiyat = null;
        if (MaliyetGerekliMi(tur))
        {
            if (girilenBirimFiyat is not { } fiyat || fiyat < 0)
            {
                return StokHatalari.MaliyetGerekli;
            }

            birimFiyat = Math.Round(fiyat / katsayi, MiktarOndalik, MidpointRounding.AwayFromZero);
        }

        var miktar = Math.Round(girilenMiktar * katsayi, MiktarOndalik, MidpointRounding.AwayFromZero);
        var giris = GirisMi(tur);
        return new StokHareket
        {
            StokKartiId = kart.Id,
            DepoId = depoId,
            Tarih = tarih,
            Tur = tur,
            GirisMiktari = giris ? miktar : 0m,
            CikisMiktari = giris ? 0m : miktar,
            GirilenBirim = girilenBirim,
            GirilenMiktar = girilenMiktar,
            BirimFiyat = birimFiyat,
            BelgeNo = belgeNo,
            Aciklama = aciklama,
            TransferId = transferId,
            KaynakBelgeId = kaynakBelgeId
        };
    }

    // Ters kayıt aynı tarih, depo ve fiyatla atılır.
    public Sonuc<StokHareket> IptalEt()
    {
        if (IptalKaydiMi)
        {
            return StokHatalari.IptalKaydiIptalEdilemez;
        }

        if (IptalEdildiMi)
        {
            return StokHatalari.ZatenIptalEdilmis;
        }

        var ters = new StokHareket
        {
            StokKartiId = StokKartiId,
            DepoId = DepoId,
            Tarih = Tarih,
            Tur = Tur,
            GirisMiktari = CikisMiktari,
            CikisMiktari = GirisMiktari,
            GirilenBirim = GirilenBirim,
            GirilenMiktar = GirilenMiktar,
            BirimFiyat = BirimFiyat,
            BelgeNo = BelgeNo,
            Aciklama = $"İptal: {Aciklama}".TrimEnd(' ', ':'),
            TransferId = TransferId,
            KaynakBelgeId = KaynakBelgeId,
            IptalEdilenHareketId = Id
        };

        IptalHareketId = ters.Id;
        return ters;
    }
}
