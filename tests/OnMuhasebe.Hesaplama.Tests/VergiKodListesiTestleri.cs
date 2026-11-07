using OnMuhasebe.Shared.Contracts.Vergi;

namespace OnMuhasebe.Hesaplama.Tests;

// Kod listeleri GİB UBL-TR Kod Listeleri V1.43'e karşı: yanlış oran faturada eksik/fazla KDV tevkifatı demektir.
public sealed class VergiKodListesiTestleri
{
    [Theory]
    [InlineData("601", 4)]
    [InlineData("602", 9)]
    [InlineData("603", 7)]
    [InlineData("604", 5)]
    [InlineData("605", 5)]
    [InlineData("606", 9)]
    [InlineData("607", 9)]
    [InlineData("608", 9)]
    [InlineData("609", 7)]
    [InlineData("610", 9)]
    [InlineData("611", 9)]
    [InlineData("612", 9)]
    [InlineData("613", 9)]
    [InlineData("614", 5)]
    [InlineData("615", 7)]
    [InlineData("616", 5)]
    [InlineData("617", 7)]
    [InlineData("618", 7)]
    [InlineData("619", 7)]
    [InlineData("620", 7)]
    [InlineData("621", 9)]
    [InlineData("622", 9)]
    [InlineData("623", 5)]
    [InlineData("624", 2)]
    [InlineData("625", 3)]
    [InlineData("626", 2)]
    [InlineData("627", 5)]
    public void Kismi_tevkifat_kodlari_GIB_oranlariyla(string kod, int pay)
    {
        var t = TevkifatKodlari.Bul(kod);
        Assert.NotNull(t);
        Assert.Equal((pay, 10), (t.Pay, t.Payda));
    }

    [Fact]
    public void Tam_tevkifat_kodlari_801_825_ve_hepsi_10_10()
    {
        var tam = TevkifatKodlari.Tumu.Where(t => t.Kod.StartsWith('8')).ToList();
        Assert.Equal(Enumerable.Range(801, 25).Select(k => k.ToString()), tam.Select(t => t.Kod));
        Assert.All(tam, t => Assert.Equal((10, 10), (t.Pay, t.Payda)));
        Assert.Equal(52, TevkifatKodlari.Tumu.Count);
    }

    [Fact]
    public void Kodlar_tekil_ve_aciklamalar_dolu()
    {
        Assert.Equal(TevkifatKodlari.Tumu.Count, TevkifatKodlari.Tumu.Select(t => t.Kod).Distinct().Count());
        Assert.Equal(KdvIstisnaKodlari.Tumu.Count, KdvIstisnaKodlari.Tumu.Select(k => k.Kod).Distinct().Count());
        Assert.All(TevkifatKodlari.Tumu, t => Assert.False(string.IsNullOrWhiteSpace(t.Aciklama)));
        Assert.All(KdvIstisnaKodlari.Tumu, k => Assert.False(string.IsNullOrWhiteSpace(k.Aciklama)));
    }

    [Fact]
    public void Istisna_kodlari_turlerine_gore_GIB_araliginda()
    {
        Assert.All(KdvIstisnaKodlari.Tumu, k => Assert.Equal(k.Tur switch
        {
            KdvIstisnaTuru.Kismi => '2',
            KdvIstisnaTuru.Tam => '3',
            _ => '7'
        }, k.Kod[0]));
        Assert.Equal((39, 46, 4), (Say(KdvIstisnaTuru.Kismi), Say(KdvIstisnaTuru.Tam), Say(KdvIstisnaTuru.IhracKayitli)));
        Assert.Equal("11/1-a Mal İhracatı", KdvIstisnaKodlari.Bul("301")!.Aciklama);
        Assert.Equal("KDV - İstisna Olmayan Diğer", KdvIstisnaKodlari.Bul("351")!.Aciklama);
        Assert.Null(KdvIstisnaKodlari.Bul("203"));   // GİB listesinde olmayan kod

        static int Say(KdvIstisnaTuru tur) => KdvIstisnaKodlari.Tumu.Count(k => k.Tur == tur);
    }
}
