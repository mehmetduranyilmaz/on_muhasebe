using System.Text.Json.Serialization;

namespace OnMuhasebe.Shared.Contracts.Ayarlar;

[JsonConverter(typeof(JsonStringEnumConverter<DegisiklikIslemi>))]
public enum DegisiklikIslemi
{
    Guncelleme,
    Silme,

    // Yalnızca var olan bir kayda eklenen alt satır (ör. faturaya yeni satır); kaydın kendisinin eklenmesi yazılmaz.
    Ekleme
}

public sealed record AlanDegisikligi(string Alan, string? Eski, string? Yeni);

public sealed record DegisiklikSatiri(DateTime Zaman, string? KullaniciAdi, string Tablo, Guid KayitId, DegisiklikIslemi Islem, IReadOnlyList<AlanDegisikligi> Alanlar);

// Kilit tarihi ve öncesine ait hareket/fatura eklenemez, değiştirilemez, silinemez. Null: kilit yok.
public sealed record DonemKilidiAyari(DateOnly? KilitTarihi, string? Aciklama);
