using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Hesaplama.Tests;

public class DegisiklikHesaplayiciTestleri
{
    private static readonly HashSet<string> _haricler = ["Surum"];

    [Fact]
    public void Yalniz_degeri_degisen_ve_haric_olmayan_alanlar_listelenir()
    {
        var farklar = DegisiklikHesaplayici.Farklar(
        [
            ("Unvan", "Eski Ltd", "Yeni Ltd"),
            ("Telefon", "555", "555"),
            ("Surum", 1u, 2u),
            ("Eposta", null, "a@b.com")
        ], _haricler);

        Assert.Equal(["Unvan", "Eposta"], farklar.Select(f => f.Alan));
        Assert.Equal(("Eski Ltd", "Yeni Ltd"), (farklar[0].Eski, farklar[0].Yeni));
        Assert.Null(farklar[1].Eski);
    }

    [Fact]
    public void Ayni_metne_donusen_degerler_degisiklik_sayilmaz()
    {
        var farklar = DegisiklikHesaplayici.Farklar([("Tutar", 10.50m, 10.5m), ("Tarih", new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5))], _haricler);

        Assert.Empty(farklar);
    }

    [Fact]
    public void Degerler_kultur_bagimsiz_metne_cevrilir()
    {
        Assert.Equal("1234.56", DegisiklikHesaplayici.Metin(1234.56m));
        Assert.Equal("2026-10-21", DegisiklikHesaplayici.Metin(new DateOnly(2026, 10, 21)));
        Assert.Equal("true", DegisiklikHesaplayici.Metin(true));
        Assert.Equal("[ikili veri]", DegisiklikHesaplayici.Metin(new byte[] { 1, 2 }));
        Assert.Null(DegisiklikHesaplayici.Metin(null));
    }

    [Fact]
    public void Uzun_metin_kisaltilir()
    {
        var metin = DegisiklikHesaplayici.Metin(new string('a', 600))!;

        Assert.Equal(501, metin.Length);
        Assert.EndsWith("…", metin);
    }
}
