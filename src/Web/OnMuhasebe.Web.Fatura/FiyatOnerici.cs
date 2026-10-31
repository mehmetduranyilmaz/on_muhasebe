using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Web.Api;

namespace OnMuhasebe.Web.Fatura;

// Belgenin fiyat bağlamı: tarafı (satış/alış), carisi ve grubu (fiyat listesi seçimi), dövizi ve kuru.
public sealed record FiyatBaglami(bool SatisMi, bool PerakendeMi, Guid? CariId, Guid? CariGrupId, string ParaBirimi, decimal? BelgeKuru, DateOnly Tarih);

// Satıra önerilen fiyat: kademeler temel birim başına, satırın KDV seçimine ve belgenin dövizine çevrilmiştir.
// ListeAdi boşsa öneri kart fiyatıdır (tek kademe).
public sealed record FiyatOnerisi(IReadOnlyList<FiyatKademesi> Kademeler, string? ListeAdi)
{
    public FiyatKademesi? Kademe(decimal temelMiktar) => FiyatListesiKurallari.Kademe(Kademeler, temelMiktar);
}

// Satır fiyatı önerisi (teklif, sipariş, fatura). Önce carinin geçerli fiyat listesi (cariye özel > grup > genel), yoksa kart fiyatı:
// satışta toptan/perakende, alışta alış fiyatı. Liste kademesi miktara ulaşmıyorsa altındaki miktarlar için kart fiyatı geçerlidir.
public sealed class FiyatOnerici(ApiIstemcisi api)
{
    public async Task<FiyatOnerisi?> OnerAsync(StokKartiDetayi kart, FiyatBaglami b, bool satirKdvDahil)
    {
        var kartFiyati = await KartFiyatiAsync(kart, b, satirKdvDahil);
        var tur = b.SatisMi ? FiyatListesiTuru.Satis : FiyatListesiTuru.Alis;
        var adres = $"api/stok/fiyat-listeleri/uygulanan?stokKartiId={kart.Id}&tarih={Bicim.Iso(b.Tarih)}&tur={tur}"
                    + (b.CariId is { } c ? $"&cariId={c}" : "") + (b.CariGrupId is { } g ? $"&cariGrupId={g}" : "");
        var liste = await api.GetAsync<List<UygulananFiyat>>(adres) is { BasariliMi: true } s ? s.Deger.FirstOrDefault() : null;
        if (liste is null)
        {
            return kartFiyati is { } f ? new FiyatOnerisi([new FiyatKademesi(0m, f, 0m)], null) : null;
        }

        var kademeler = new List<FiyatKademesi>();
        foreach (var k in liste.Kademeler)
        {
            var fiyat = KdvCevir(k.Fiyat, liste.KdvDahilMi, satirKdvDahil, kart.KdvOrani);
            if (await CevirAsync(fiyat, liste.ParaBirimi, b.ParaBirimi, b.BelgeKuru, b.Tarih) is { } cevrilen)
            {
                kademeler.Add(k with { Fiyat = cevrilen });
            }
        }

        if (kademeler.Count == 0)
        {
            return kartFiyati is { } f ? new FiyatOnerisi([new FiyatKademesi(0m, f, 0m)], null) : null;
        }

        if (kademeler.Min(k => k.MinMiktar) > 0m && kartFiyati is { } taban)
        {
            kademeler.Insert(0, new FiyatKademesi(0m, taban, 0m));
        }

        return new FiyatOnerisi(kademeler, liste.ListeAdi);
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

    // Perakende fiyatı KDV dahil, toptan ve alış fiyatı KDV hariçtir.
    private async Task<decimal?> KartFiyatiAsync(StokKartiDetayi kart, FiyatBaglami b, bool satirKdvDahil)
    {
        var perakende = b.SatisMi && b.PerakendeMi && kart.PerakendeFiyati is not null;
        decimal? fiyat = !b.SatisMi ? kart.AlisFiyati : perakende ? kart.PerakendeFiyati : kart.SatisFiyati;
        return fiyat is { } f
            ? await CevirAsync(KdvCevir(f, perakende, satirKdvDahil, kart.KdvOrani), kart.ParaBirimi, b.ParaBirimi, b.BelgeKuru, b.Tarih)
            : null;
    }

    private static decimal KdvCevir(decimal fiyat, bool kaynakKdvDahil, bool hedefKdvDahil, int kdvOrani)
    {
        if (kaynakKdvDahil == hedefKdvDahil)
        {
            return fiyat;
        }

        var carpan = 1m + kdvOrani / 100m;
        return kaynakKdvDahil ? fiyat / carpan : fiyat * carpan;
    }

    private async Task<decimal?> KurAsync(string paraBirimi, DateOnly tarih)
    {
        var sonuc = await api.GetAsync<KurBilgisi>($"api/doviz/islem-kuru?paraBirimi={paraBirimi}&tarih={Bicim.Iso(tarih)}");
        return sonuc.BasariliMi ? sonuc.Deger.DovizAlis : null;
    }
}
