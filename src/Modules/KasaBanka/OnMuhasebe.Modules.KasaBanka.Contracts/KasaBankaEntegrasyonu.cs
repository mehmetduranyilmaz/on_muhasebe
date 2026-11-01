using OnMuhasebe.Shared.Contracts.Muhasebe;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Diğer modüllerin (fatura) kasa/bankayla konuştuğu sözleşmeler.

public sealed record KasaHesapBilgisi(Guid Id, string Kod, string Ad, HesapTuru Tur, string ParaBirimi, bool AktifMi);

public interface IKasaBankaSorgusu
{
    // Muhasebe aktarımı: kasa/banka/kart/POS hesapları ve gelir-gider kategorileri (pasifler dahil).
    Task<IReadOnlyList<MuhasebeKarti>> HesapKartlariAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MuhasebeKarti>> KategoriKartlariAsync(CancellationToken cancellationToken = default);

    // İptal edilmemiş hareketler (iptal edilen ve iptal kaydı birlikte düşer); açılış hareketleri muhasebeye aktarılmaz.
    Task<IReadOnlyList<MuhasebeKasaHareketi>> MuhasebeHareketleriAsync(
        DateOnly baslangic, DateOnly bitis, Guid? isyeriId, CancellationToken cancellationToken = default);

    // Çek/senet ve tahakkuk kayıtları işyerine bağlı değildir: firma geneli döner.
    Task<IReadOnlyList<MuhasebeEvrakHareketi>> MuhasebeEvrakHareketleriAsync(DateOnly baslangic, DateOnly bitis, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MuhasebeTahakkuk>> MuhasebeTahakkuklariAsync(DateOnly baslangic, DateOnly bitis, CancellationToken cancellationToken = default);

    Task<KasaHesapBilgisi?> HesapGetirAsync(Guid hesapId, CancellationToken cancellationToken = default);

    // Belgeye atanacak masraf merkezi firmanın aktif merkezi olmalıdır.
    Task<bool> MasrafMerkeziAktifMiAsync(Guid masrafMerkeziId, CancellationToken cancellationToken = default);
}

// Faturanın peşin tahsilatı/ödemesi. Tutar ve kur hesabın (= faturanın) dövizindedir. Cari varsa cari hareketi de yazılır
// ve önce kaynak belgenin kalemini kapatır; cari yoksa (nihai tüketici) yalnızca kasa/banka etkilenir.
public sealed record BelgeTahsilati(
    Guid HesapId,
    Guid? CariHesapId,
    bool GirisMi,
    DateOnly Tarih,
    decimal Tutar,
    decimal Kur,
    string? BelgeNo,
    string? Aciklama);

public interface IKasaBankaYazici
{
    Task<Sonuc> BelgeTahsilatiYazAsync(Guid kaynakBelgeId, BelgeTahsilati tahsilat, CancellationToken cancellationToken = default);

    // Belgeye bağlı tüm kasa/banka işlemleri (ve onların cari hareketleri) ters kayıtla iptal edilir.
    Task<Sonuc> BelgeIslemleriniIptalEtAsync(Guid kaynakBelgeId, CancellationToken cancellationToken = default);
}
