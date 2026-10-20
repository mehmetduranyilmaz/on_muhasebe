namespace OnMuhasebe.Web.Cari;

// Kasa/Banka modülünün cari çek/senet riski cevabı. Cari ekran paketi Kasa/Banka sözleşmelerine bağlanmasın diye yerel tanım;
// alan adları API cevabıyla aynıdır. Risk limitine portföydeki ve ciro edilip vadesi gelmemiş evraklar eklenir.
public sealed record CekRiski(string ParaBirimi, decimal Portfoyde, decimal CiroRiski, decimal Karsiliksiz, decimal VerilenAcik);
