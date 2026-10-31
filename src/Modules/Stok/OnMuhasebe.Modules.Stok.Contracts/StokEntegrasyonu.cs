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
    bool AktifMi,
    IzlemeTuru Izleme = IzlemeTuru.Yok,
    bool SktTakibi = false)
{
    public bool BirimTanimliMi(string birim) => birim == TemelBirim || EkBirimler.Any(b => b.BirimKodu == birim);

    // 1 {birim} kaç temel birimdir; tanımsız birim temel birim sayılır.
    public decimal Katsayi(string birim) => birim == TemelBirim ? 1m : EkBirimler.FirstOrDefault(b => b.BirimKodu == birim)?.Katsayi ?? 1m;
}

public interface IStokSorgusu
{
    Task<IReadOnlyList<StokKartiBilgisi>> KartlariGetirAsync(IReadOnlyCollection<Guid> kartIdleri, CancellationToken cancellationToken = default);

    // Depo verilmezse firmanın varsayılan deposu kullanılır (yoksa açılır). Pasif ya da başka firmanın deposu null döner.
    Task<Guid?> DepoCozAsync(Guid? depoId, CancellationToken cancellationToken = default);

    // Belgelerin (fatura, irsaliye) stok hareketlerinin maliyeti, firmanın maliyet yöntemiyle (kârlılık için).
    Task<IReadOnlyList<BelgeMaliyeti>> BelgeMaliyetleriAsync(IReadOnlyCollection<Guid> kaynakBelgeIdleri, CancellationToken cancellationToken = default);
}

// Bir belgedeki bir kartın stok etkisi. Çıkışta (satış) maliyet çıkış anındaki birim maliyettir; satıştan iade girişinde
// stoğa giriş maliyetidir. Miktar temel birimdedir, MaliyetTl toplamdır. İptal edilmiş hareketler sayılmaz.
public sealed record BelgeMaliyeti(Guid KaynakBelgeId, Guid StokKartiId, decimal CikisMiktari, decimal CikisMaliyetiTl, decimal GirisMiktari, decimal GirisMaliyetiTl)
{
    public decimal CikisBirimMaliyeti => CikisMiktari == 0 ? 0m : CikisMaliyetiTl / CikisMiktari;
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
    string? Aciklama,
    string? LotNo = null,
    DateOnly? SonKullanmaTarihi = null,
    IReadOnlyList<string>? SeriNolari = null);

// Kaynak belgeye bağlı hareketler yalnızca bu sözleşmeyle yazılır ve iptal edilir; elle iptal edilemez.
public interface IStokHareketYazici
{
    // Eksi stok ayarı uygulanır: "Engelle"de hata, "Uyar"da başarı ile birlikte uyarı metni döner.
    Task<Sonuc<string?>> YazAsync(Guid kaynakBelgeId, IReadOnlyList<BelgeStokHareketi> hareketler, CancellationToken cancellationToken = default);

    Task<Sonuc> IptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default);
}
