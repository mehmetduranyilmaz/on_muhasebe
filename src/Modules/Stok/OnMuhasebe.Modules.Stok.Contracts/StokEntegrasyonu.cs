using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Stok.Contracts;

// Diğer modüllerin (fatura) stokla konuştuğu sözleşmeler. Stok'un iç tiplerine erişim yalnızca bunlar üzerindendir.

public sealed record StokKartiBilgisi(
    Guid Id,
    string Kod,
    string Ad,
    StokTuru Tur,
    string TemelBirim,
    IReadOnlyList<EkBirim> EkBirimler,
    int KdvOrani,
    bool AktifMi)
{
    public bool BirimTanimliMi(string birim) => birim == TemelBirim || EkBirimler.Any(b => b.BirimKodu == birim);
}

public interface IStokSorgusu
{
    Task<IReadOnlyList<StokKartiBilgisi>> KartlariGetirAsync(IReadOnlyCollection<Guid> kartIdleri, CancellationToken cancellationToken = default);

    // Depo verilmezse firmanın varsayılan deposu kullanılır (yoksa açılır). Pasif ya da başka firmanın deposu null döner.
    Task<Guid?> DepoCozAsync(Guid? depoId, CancellationToken cancellationToken = default);
}

// Miktar ve birim fiyat girilen birim üzerindendir. BirimFiyat giriş hareketlerinde TL maliyettir;
// satıştan iadede boş bırakılırsa iade tarihindeki birim maliyet kullanılır.
public sealed record BelgeStokHareketi(
    Guid StokKartiId,
    Guid DepoId,
    StokHareketTuru Tur,
    DateOnly Tarih,
    string Birim,
    decimal Miktar,
    decimal? BirimFiyat,
    string? BelgeNo,
    string? Aciklama);

// Kaynak belgeye bağlı hareketler yalnızca bu sözleşmeyle yazılır ve iptal edilir; elle iptal edilemez.
public interface IStokHareketYazici
{
    // Eksi stok ayarı uygulanır: "Engelle"de hata, "Uyar"da başarı ile birlikte uyarı metni döner.
    Task<Sonuc<string?>> YazAsync(Guid kaynakBelgeId, IReadOnlyList<BelgeStokHareketi> hareketler, CancellationToken cancellationToken = default);

    Task<Sonuc> IptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default);
}
