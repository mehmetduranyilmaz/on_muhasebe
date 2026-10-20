namespace OnMuhasebe.Web.Bilesenler;

public sealed record GrafikDilimi(string Etiket, decimal Deger, string DegerMetni);

public sealed record CubukOgesi(string Etiket, decimal Deger, string DegerMetni, string? Href = null);
