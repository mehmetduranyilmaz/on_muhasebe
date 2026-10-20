using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<EkstreBicimi>))]
public enum EkstreBicimi
{
    Csv,
    Excel,
    Mt940
}

[JsonConverter(typeof(JsonStringEnumConverter<AktarimDurumu>))]
public enum AktarimDurumu
{
    // Excel/CSV yüklendi; kolonlar eşlenmeli.
    EslemeBekliyor,
    // Satırlar çözüldü; kullanıcı önerileri gözden geçirip aktarır.
    OnayBekliyor,
    Tamamlandi
}

[JsonConverter(typeof(JsonStringEnumConverter<EkstreSatirDurumu>))]
public enum EkstreSatirDurumu
{
    Bekliyor,
    Aktarildi,
    Atlandi,
    // Aynı hesapta elle girilmiş eşdeğer bir hareket bulundu; yeniden yazılmaz, ona bağlanır.
    ZatenKayitli,
    // Bu satır daha önceki bir aktarımda aktarılmış (aynı dosya ya da çakışan dönem tekrar yüklendi).
    DahaOnceAktarildi,
    Hatali
}

[JsonConverter(typeof(JsonStringEnumConverter<EkstreIslemTuru>))]
public enum EkstreIslemTuru
{
    Tahsilat,
    Odeme,
    Gelir,
    Gider,
    Virman,
    Atla
}

[JsonConverter(typeof(JsonStringEnumConverter<KuralYonu>))]
public enum KuralYonu
{
    Herhangi,
    Giris,
    Cikis
}

// Kolon numaraları 0 tabanlıdır. BaslikSatiri: başlıkların bulunduğu satır (0 tabanlı, -1 = başlık yok); veri bir alt satırdan başlar.
// Tutar ya tek işaretli kolondan (TutarKolonu) ya da ayrı giriş (AlacakKolonu) / çıkış (BorcKolonu) kolonlarından okunur.
// TutarTersMi: kart ekstrelerindeki gibi pozitif tutar harcamayı (çıkışı) gösteriyorsa.
public sealed record KolonEslemesi(
    int BaslikSatiri,
    int TarihKolonu,
    int AciklamaKolonu,
    int? TutarKolonu,
    int? AlacakKolonu,
    int? BorcKolonu,
    int? BakiyeKolonu,
    int? ReferansKolonu,
    bool TutarTersMi);

public sealed record EslemeIstegi(KolonEslemesi Esleme, bool SablonuKaydet);

public sealed record EkstreSatiriDetayi(
    Guid Id,
    int Sira,
    DateOnly Tarih,
    string Aciklama,
    decimal Tutar,
    decimal? Bakiye,
    string? Referans,
    EkstreSatirDurumu Durum,
    EkstreIslemTuru IslemTuru,
    Guid? CariHesapId,
    string? CariUnvan,
    Guid? KategoriId,
    Guid? KarsiHesapId,
    Guid? EslesenHareketId,
    DateOnly? EslesenHareketTarihi,
    Guid? IslemId,
    string? OneriKaynagi,
    string? Hata,
    bool ElleDuzenlendi);

// IlkSatirlar: kolon eşleme ekranı için dosyanın ilk satırları (Excel/CSV).
public sealed record AktarimDetayi(
    Guid Id,
    Guid HesapId,
    string HesapAdi,
    string ParaBirimi,
    string DosyaAdi,
    EkstreBicimi Bicim,
    AktarimDurumu Durum,
    DateTime OlusturmaTarihi,
    DateOnly? Baslangic,
    DateOnly? Bitis,
    string? BakiyeUyarisi,
    KolonEslemesi? Esleme,
    IReadOnlyList<IReadOnlyList<string>> IlkSatirlar,
    IReadOnlyList<EkstreSatiriDetayi> Satirlar);

public sealed record AktarimOzeti(
    Guid Id,
    string DosyaAdi,
    EkstreBicimi Bicim,
    AktarimDurumu Durum,
    DateTime OlusturmaTarihi,
    DateOnly? Baslangic,
    DateOnly? Bitis,
    int SatirSayisi,
    int AktarilanSayisi);

// Satır seçimi değiştirilir. HatirlanacakKelime verilirse açıklamada bu kelime geçen satırlar için kural kaydedilir
// ve aynı aktarımdaki elle düzenlenmemiş diğer bekleyen satırlara hemen uygulanır.
// EslesmeyiKaldir: "zaten kayıtlı" eşleşmesi kaldırılır, satır yeni hareket olarak aktarılır.
public sealed record SatirGuncelleIstegi(
    EkstreIslemTuru IslemTuru,
    Guid? CariHesapId,
    Guid? KategoriId,
    Guid? KarsiHesapId,
    bool EslesmeyiKaldir,
    string? HatirlanacakKelime);

public sealed record AktarimSonucu(int Aktarilan, int ZatenKayitli, int Atlanan, int Hatali, AktarimDetayi Aktarim);

public sealed record EslestirmeKurali(
    Guid Id,
    string AnahtarKelime,
    KuralYonu Yon,
    EkstreIslemTuru IslemTuru,
    Guid? CariHesapId,
    string? CariUnvan,
    Guid? KategoriId,
    string? KategoriAdi,
    Guid? KarsiHesapId,
    string? KarsiHesapAdi,
    int KullanimSayisi);

public sealed record KuralKaydetIstegi(
    string AnahtarKelime,
    KuralYonu Yon,
    EkstreIslemTuru IslemTuru,
    Guid? CariHesapId,
    Guid? KategoriId,
    Guid? KarsiHesapId);
