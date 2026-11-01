namespace OnMuhasebe.Shared.Contracts.Muhasebe;

// Kaynak modüllerin muhasebe aktarımına sunduğu kart bilgisi (cari, kasa/banka hesabı, gelir-gider kategorisi).
// Tur: cari için Musteri/Tedarikci/HerIkisi, hesap için Kasa/Banka/KrediKarti/Pos,
// kategori için Gelir/Gider/Finansman (kredi faiz ve masrafları).
public sealed record MuhasebeKarti(Guid Id, string Kod, string Ad, string Tur);

// Onaylı faturanın muhasebeleştirilecek tutarları (fatura dövizinde; Kur ile TL'ye çevrilir).
// Tur: FaturaTuru adı. StokluMatrah: stok kartlı satırların, StoksuzMatrah: hizmet/serbest satırların matrahı.
public sealed record MuhasebeFaturasi(
    Guid Id, string Tur, string? Numara, DateOnly Tarih, Guid? CariHesapId, string CariUnvan, decimal Kur,
    decimal StokluMatrah, decimal StoksuzMatrah, decimal Kdv, decimal TevkifEdilenKdv, decimal Stopaj, Guid IsyeriId, string? Aciklama);

// İptal edilmemiş kasa/banka hareketi (TL). Tur: KasaHareketTuru adı. KarsiRol: karşı hesabı kaynak modül biliyorsa (ör. çek ise
// "AlinanCekler", senet ise "AlacakSenetleri") HesapRolu adı; boşsa muhasebe cari/kategori/hareket türünden belirler.
// GiderTl: tutarın gidere ayrılan kısmı (kredi taksitinde faiz ve vergiler, GiderKategoriId kategorisine); kalanı karşı hesaba.
public sealed record MuhasebeKasaHareketi(
    Guid Id, Guid IslemId, DateOnly Tarih, string Tur, Guid HesapId, decimal GirisTl, decimal CikisTl,
    Guid? CariHesapId, Guid? KategoriId, string? KarsiRol, string? BelgeNo, string? Aciklama, Guid? IsyeriId,
    decimal GiderTl = 0, Guid? GiderKategoriId = null);
