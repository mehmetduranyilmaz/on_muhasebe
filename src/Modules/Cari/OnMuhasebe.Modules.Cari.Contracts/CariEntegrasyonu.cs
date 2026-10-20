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
    bool AktifMi,
    bool FarkliDovizdeIslem);

public sealed record CariEslestirmeAdayi(Guid Id, string Unvan, string? VergiNo, string? Iban, string ParaBirimi);

public interface ICariSorgusu
{
    Task<CariBilgisi?> GetirAsync(Guid cariId, CancellationToken cancellationToken = default);

    // Belgelerin (fatura vb.) kapanmamış tutarı, carinin para biriminde. Cari hareketi olmayan ya da iptal edilmiş belge listede yoktur.
    Task<IReadOnlyDictionary<Guid, decimal>> BelgeKalanlariAsync(IReadOnlyCollection<Guid> belgeIdleri, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> UnvanlarAsync(IReadOnlyCollection<Guid> cariIdleri, CancellationToken cancellationToken = default);

    // Banka ekstresi önerileri için firmanın aktif carileri (açıklamada VKN, IBAN veya unvan aranır).
    Task<IReadOnlyList<CariEslestirmeAdayi>> EslestirmeAdaylariAsync(CancellationToken cancellationToken = default);
}

// Tutar carinin para birimindedir; TL carilerde kur 1'dir.
// OtomatikKapama false ise kalem yazılınca FIFO çalışmaz (ör. peşin faturayı hemen ardından gelen tahsilat kapatacaktır).
public sealed record BelgeCariHareketi(
    Guid CariHesapId,
    CariHareketTuru Tur,
    HareketYonu Yon,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    decimal Tutar,
    decimal Kur,
    string? BelgeNo,
    string? Aciklama,
    Guid? OncelikliKapamaBelgeId = null,
    bool OtomatikKapama = true);

// Kaynak belgeye bağlı hareketler yalnızca bu sözleşmeyle yazılır ve iptal edilir; elle iptal edilemez.
// Birden çok modülü kapsayan işlemlerde çağıran taraf IIslemKapsami ile tek transaction açar.
public interface ICariHareketYazici
{
    Task<Sonuc> YazAsync(Guid kaynakBelgeId, BelgeCariHareketi hareket, CancellationToken cancellationToken = default);

    Task<Sonuc> IptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default);
}
