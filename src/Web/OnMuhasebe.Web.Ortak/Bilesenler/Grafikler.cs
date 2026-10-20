namespace OnMuhasebe.Web.Bilesenler;

public sealed record GrafikDilimi(string Etiket, decimal Deger, string DegerMetni);

public sealed record CubukOgesi(string Etiket, decimal Deger, string DegerMetni, string? Href = null);

// Sütun grafiği: her grup (ör. ay) için serilerin (ör. satış, alış) değerleri, seri sırasıyla.
public sealed record SutunGrubu(string Etiket, IReadOnlyList<decimal> Degerler);

public sealed record SutunSerisi(string Ad, string Renk);
