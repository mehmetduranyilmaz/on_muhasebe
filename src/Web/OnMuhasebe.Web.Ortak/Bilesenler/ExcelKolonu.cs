namespace OnMuhasebe.Web.Bilesenler;

// Listenin Excel'e aktarılan bir sütunu. Değer string, sayı (int/long/decimal), bool, DateOnly, DateTime ya da null olabilir;
// tutarlar sayı olarak verilmelidir ki Excel'de toplanabilsin (biçimlenmiş metin olarak değil).
public sealed record ExcelKolonu<T>(string Baslik, Func<T, object?> Deger, int Genislik = 18);
