using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Doviz.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure;

internal sealed record CariTutari(string Unvan, decimal Tutar, decimal Kur);

// Kasa/banka ve çek/senet işlemlerinin ortak kur ve cari kuralları.
internal sealed class CariDovizServisi(
    ICariSorgusu cariSorgusu,
    IKurSorgusu kurSorgusu,
    ILisansSorgusu lisansSorgusu,
    IMevcutKullanici mevcutKullanici)
{
    // TL için 1; dövizde girilen kur ya da işlem tarihinin TCMB döviz alış kuru.
    public async Task<Sonuc<decimal>> KurAsync(string paraBirimi, DateOnly tarih, decimal? girilen, CancellationToken ct)
    {
        if (paraBirimi == ParaBirimleri.Tl)
        {
            return 1m;
        }

        if (girilen is > 0)
        {
            return girilen.Value;
        }

        return (await kurSorgusu.IslemKuruAsync(paraBirimi, tarih, ct))?.DovizAlis is { } kur ? kur : KasaBankaHatalari.KurGerekli;
    }

    public async Task<Sonuc<CariBilgisi>> CariAsync(Guid cariId, CancellationToken ct)
    {
        var lisans = await lisansSorgusu.GetirAsync(mevcutKullanici.FirmaId!.Value, ct);
        if (lisans?.Moduller.Contains(ModulKodlari.Cari) != true)
        {
            return KasaBankaHatalari.CariLisansiYok;
        }

        var cari = await cariSorgusu.GetirAsync(cariId, ct);
        if (cari is null)
        {
            return KasaBankaHatalari.CariBulunamadi;
        }

        return cari.AktifMi ? cari : KasaBankaHatalari.PasifCari;
    }

    // Tutar işlemin dövizindedir. Cari aynı dövizdeyse aynen, farklıysa (cari izin veriyorsa) TL karşılığı üzerinden cari dövizine çevrilir.
    public async Task<Sonuc<CariTutari>> CariTutariAsync(
        Guid cariId, string paraBirimi, decimal tutar, decimal kur, decimal? cariKuru, DateOnly tarih, CancellationToken ct)
    {
        var cariSonuc = await CariAsync(cariId, ct);
        if (!cariSonuc.BasariliMi)
        {
            return cariSonuc.Hata;
        }

        var cari = cariSonuc.Deger;
        if (cari.ParaBirimi == paraBirimi)
        {
            return new CariTutari(cari.Unvan, tutar, kur);
        }

        if (!cari.FarkliDovizdeIslem)
        {
            return KasaBankaHatalari.FarkliDoviz;
        }

        var cariKur = await KurAsync(cari.ParaBirimi, tarih, cariKuru, ct);
        return cariKur.BasariliMi ? new CariTutari(cari.Unvan, Para.Yuvarla(tutar * kur / cariKur.Deger), cariKur.Deger) : cariKur.Hata;
    }
}
