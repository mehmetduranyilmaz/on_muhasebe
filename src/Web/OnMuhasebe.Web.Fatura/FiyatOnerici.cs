using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Web.Api;

namespace OnMuhasebe.Web.Fatura;

// Satır fiyatı önerisi (teklif, sipariş, fatura). Satışta toptan/perakende, alışta alış fiyatı; perakende fiyatı KDV dahil, diğerleri hariçtir.
// Öneri satırın KDV dahil seçimine ve belgenin dövizine çevrilir (çapraz kur TL üzerinden, belge tarihinin TCMB kuruyla).
public sealed class FiyatOnerici(ApiIstemcisi api)
{
    public async Task<decimal?> OnerAsync(
        StokKartiDetayi kart, bool satisMi, bool perakendeMi, bool satirKdvDahil, string paraBirimi, decimal? belgeKuru, DateOnly tarih)
    {
        var perakende = satisMi && perakendeMi && kart.PerakendeFiyati is not null;
        decimal? fiyat = !satisMi ? kart.AlisFiyati : perakende ? kart.PerakendeFiyati : kart.SatisFiyati;
        if (fiyat is not { } f)
        {
            return null;
        }

        var kdvCarpani = 1m + kart.KdvOrani / 100m;
        if (perakende != satirKdvDahil)
        {
            f = perakende ? f / kdvCarpani : f * kdvCarpani;
        }

        return await CevirAsync(f, kart.ParaBirimi, paraBirimi, belgeKuru, tarih);
    }

    public async Task<decimal?> CevirAsync(decimal tutar, string kaynakPara, string hedefPara, decimal? hedefKuru, DateOnly tarih)
    {
        if (kaynakPara != hedefPara)
        {
            var kaynakKuru = kaynakPara == ParaBirimleri.Tl ? 1m : await KurAsync(kaynakPara, tarih);
            var hedef = hedefPara == ParaBirimleri.Tl ? 1m : hedefKuru;
            if (kaynakKuru is not { } kk || hedef is not > 0)
            {
                return null;
            }

            tutar = tutar * kk / hedef.Value;
        }

        return Math.Round(tutar, 4, MidpointRounding.AwayFromZero);
    }

    private async Task<decimal?> KurAsync(string paraBirimi, DateOnly tarih)
    {
        var sonuc = await api.GetAsync<KurBilgisi>($"api/doviz/islem-kuru?paraBirimi={paraBirimi}&tarih={Bicim.Iso(tarih)}");
        return sonuc.BasariliMi ? sonuc.Deger.DovizAlis : null;
    }
}
