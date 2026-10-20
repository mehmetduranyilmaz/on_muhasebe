using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Cari.Contracts;

// Diğer modüllerin (fatura, kasa/banka) cariyle konuştuğu sözleşmeler. Cari'nin iç tiplerine erişim yalnızca bunlar üzerindendir.

public sealed record CariBilgisi(
    Guid Id,
    string Kod,
    string Unvan,
    string? VergiNo,
    string? VergiDairesi,
    string? Il,
    string? Ilce,
    string? Adres,
    string ParaBirimi,
    bool AktifMi);

public interface ICariSorgusu
{
    Task<CariBilgisi?> GetirAsync(Guid cariId, CancellationToken cancellationToken = default);
}

// Tutar carinin para birimindedir; TL carilerde kur 1'dir.
public sealed record BelgeCariHareketi(
    Guid CariHesapId,
    CariHareketTuru Tur,
    HareketYonu Yon,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    decimal Tutar,
    decimal Kur,
    string? BelgeNo,
    string? Aciklama);

// Kaynak belgeye bağlı hareketler yalnızca bu sözleşmeyle yazılır ve iptal edilir; elle iptal edilemez.
// Birden çok modülü kapsayan işlemlerde çağıran taraf IIslemKapsami ile tek transaction açar.
public interface ICariHareketYazici
{
    Task<Sonuc> YazAsync(Guid kaynakBelgeId, BelgeCariHareketi hareket, CancellationToken cancellationToken = default);

    Task<Sonuc> IptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default);
}
