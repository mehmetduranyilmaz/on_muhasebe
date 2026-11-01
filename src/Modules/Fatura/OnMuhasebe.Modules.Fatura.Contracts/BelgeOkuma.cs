using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.Fatura.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<BelgeKaynagi>))]
public enum BelgeKaynagi { Xml, Karekod }

// Gelen e-Fatura/e-Arşiv belgesinden okunan bilgi. Karekodda satır yoktur: KDV oranı başına bir toplam satırı üretilir.
public sealed record OkunanBelge(
    BelgeKaynagi Kaynak,
    FaturaTuru Tur,
    string Numara,
    string? Ettn,
    DateOnly Tarih,
    DateOnly? VadeTarihi,
    string ParaBirimi,
    decimal? Kur,
    string? TedarikciVkn,
    string? TedarikciUnvan,
    string? VergiDairesi,
    string? AliciVkn,
    decimal? Odenecek,
    IReadOnlyList<FaturaSatiriIstegi> Satirlar,
    IReadOnlyList<string> Uyarilar);

// CariHesapId: VKN ile bulunan cari; yoksa kullanıcı seçer ya da yeni kart açar. MevcutFaturaId: aynı cari ve numarayla kayıtlı fatura.
public sealed record BelgeOnizleme(
    OkunanBelge Belge,
    Guid? CariHesapId,
    string? CariUnvan,
    Guid? MevcutFaturaId,
    int EslesenStokSatiri,
    IReadOnlyList<string> Uyarilar);

public sealed record BelgeKarekodIstegi(string Metin);

public sealed record BelgedenFaturaIstegi(OkunanBelge Belge, Guid CariHesapId, Guid? IsyeriId);
