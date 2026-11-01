using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Fatura.Infrastructure;

// Satın alma talebinden taslak sipariş. Kurallar elle açılan siparişle aynıdır: kaydı TeklifSiparisServisi yapar.
internal sealed class SatinalmaSiparisiServisi(TeklifSiparisServisi servis, FaturaDbContext db) : ISatinalmaSiparisiServisi
{
    public async Task<Sonuc<Guid>> TaslakAcAsync(SatinalmaSiparisiTaslagi taslak, CancellationToken cancellationToken = default)
    {
        var sonuc = await servis.OlusturAsync(new TeklifSiparisKaydetIstegi(
            TeklifSiparisTuru.SatinalmaSiparisi, taslak.TedarikciId, null, taslak.Tarih, null, taslak.TerminTarihi, ParaBirimleri.Tl, null,
            GenelIskontoTuru.Yuzde, 0, null, null, taslak.Aciklama, taslak.Satirlar, IsyeriId: taslak.IsyeriId), cancellationToken);
        return sonuc.BasariliMi ? sonuc.Deger.Id : sonuc.Hata;
    }

    public async Task<SatinalmaSiparisiOzeti?> GetirAsync(Guid siparisId, CancellationToken cancellationToken = default)
    {
        var s = await db.TeklifSiparisler.AsNoTracking().Where(t => t.Id == siparisId)
            .Select(t => new { t.Id, t.Numara, t.Durum, t.Matrah, t.Kur }).SingleOrDefaultAsync(cancellationToken);
        return s is null ? null : new SatinalmaSiparisiOzeti(s.Id, s.Numara, s.Durum, Para.TlKarsiligi(s.Matrah, s.Kur));
    }
}
