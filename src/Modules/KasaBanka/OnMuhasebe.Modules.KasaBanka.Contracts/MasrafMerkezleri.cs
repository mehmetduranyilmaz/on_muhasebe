namespace OnMuhasebe.Modules.KasaBanka.Contracts;

// Masraf merkezi (proje, şube, departman, araç...): gelir ve giderin hangi faaliyete ait olduğunu gösterir.
// Faturaya (belge düzeyinde) ve kasa/banka gelir-gider kaydına atanır; muhasebe kaydını değiştirmez, raporlamayı böler.
public sealed record MasrafMerkeziOzeti(Guid Id, string Kod, string Ad, bool AktifMi);

public sealed record MasrafMerkeziKaydetIstegi(string Kod, string Ad);

// Bir merkezin dönem tutarları (TL, KDV hariç). MasrafMerkeziId boşsa merkez atanmamış kayıtlardır.
public sealed record MerkezTutari(Guid? MasrafMerkeziId, decimal GelirTl, decimal GiderTl, int Adet);

public sealed record MerkezTutarlari(DateOnly Baslangic, DateOnly Bitis, IReadOnlyList<MerkezTutari> Kalemler);
