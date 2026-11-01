using System.Text;
using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Fatura.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class BelgeOkumaTestleri
{
    private const string Ubl = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:ID>abc2026000000042</cbc:ID>
          <cbc:UUID>f47ac10b-58cc-4372-a567-0e02b2c3d479</cbc:UUID>
          <cbc:IssueDate>2026-10-15</cbc:IssueDate>
          <cbc:InvoiceTypeCode>SATIS</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>TRY</cbc:DocumentCurrencyCode>
          <cac:AccountingSupplierParty><cac:Party>
            <cac:PartyIdentification><cbc:ID schemeID="MERSISNO">0123</cbc:ID></cac:PartyIdentification>
            <cac:PartyIdentification><cbc:ID schemeID="VKN">1234567890</cbc:ID></cac:PartyIdentification>
            <cac:PartyName><cbc:Name>Tedarikci Ltd</cbc:Name></cac:PartyName>
            <cac:PartyTaxScheme><cac:TaxScheme><cbc:Name>Kadikoy</cbc:Name></cac:TaxScheme></cac:PartyTaxScheme>
          </cac:Party></cac:AccountingSupplierParty>
          <cac:AccountingCustomerParty><cac:Party>
            <cac:PartyIdentification><cbc:ID schemeID="TCKN">11111111110</cbc:ID></cac:PartyIdentification>
          </cac:Party></cac:AccountingCustomerParty>
          <cac:PaymentMeans><cbc:PaymentDueDate>2026-11-15</cbc:PaymentDueDate></cac:PaymentMeans>
          <cac:LegalMonetaryTotal><cbc:PayableAmount currencyID="TRY">1188.00</cbc:PayableAmount></cac:LegalMonetaryTotal>
          <cac:InvoiceLine>
            <cbc:InvoicedQuantity unitCode="KGM">10</cbc:InvoicedQuantity>
            <cac:AllowanceCharge><cbc:ChargeIndicator>false</cbc:ChargeIndicator><cbc:Amount>100</cbc:Amount></cac:AllowanceCharge>
            <cac:TaxTotal><cac:TaxSubtotal><cbc:Percent>20</cbc:Percent></cac:TaxSubtotal></cac:TaxTotal>
            <cac:Item><cbc:Name>Zeytinyagi</cbc:Name><cac:SellersItemIdentification><cbc:ID>ZY-1</cbc:ID></cac:SellersItemIdentification></cac:Item>
            <cac:Price><cbc:PriceAmount>100</cbc:PriceAmount></cac:Price>
          </cac:InvoiceLine>
          <cac:InvoiceLine>
            <cbc:InvoicedQuantity unitCode="XYZ">1</cbc:InvoicedQuantity>
            <cac:TaxTotal><cac:TaxSubtotal><cbc:Percent>10</cbc:Percent></cac:TaxSubtotal></cac:TaxTotal>
            <cac:Item><cbc:Name>Nakliye</cbc:Name></cac:Item>
            <cac:Price><cbc:PriceAmount>0</cbc:PriceAmount></cac:Price>
          </cac:InvoiceLine>
        </Invoice>
        """;

    private static OkunanBelge Xml(string xml = Ubl) => UblOkuyucu.Oku(Encoding.UTF8.GetBytes(xml)).Deger;

    [Fact]
    public void Ubl_baslik_bilgileri_okunur()
    {
        var b = Xml();

        Assert.Equal((BelgeKaynagi.Xml, FaturaTuru.Alis, "ABC2026000000042", new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 15)), (b.Kaynak, b.Tur, b.Numara, b.Tarih, b.VadeTarihi!.Value));
        Assert.Equal(("1234567890", "Tedarikci Ltd", "Kadikoy", "11111111110", 1188m), (b.TedarikciVkn!, b.TedarikciUnvan!, b.VergiDairesi!, b.AliciVkn!, b.Odenecek!.Value));
    }

    [Fact]
    public void Ubl_satirlari_birim_iskonto_ve_kdv_ile_okunur()
    {
        var s = Xml().Satirlar[0];

        Assert.Equal(("Zeytinyagi", "KG", 10m, 100m, 10m, 20), (s.Ad!, s.Birim, s.Miktar, s.BirimFiyat, s.Iskonto1, s.KdvOrani));
    }

    [Fact]
    public void Tanimsiz_birim_uyarili_adet_olur()
    {
        var b = Xml();

        Assert.Equal("ADET", b.Satirlar[1].Birim);
        Assert.Contains(b.Uyarilar, u => u.Contains("XYZ", StringComparison.Ordinal));
    }

    [Fact]
    public void Satici_urun_kodlari_sirayla_doner() =>
        Assert.Equal(["ZY-1", null], UblOkuyucu.SatirKodlari(Encoding.UTF8.GetBytes(Ubl)));

    [Fact]
    public void Iade_tipi_satistan_iade_olur() =>
        Assert.Equal(FaturaTuru.SatistanIade, Xml(Ubl.Replace(">SATIS<", ">IADE<", StringComparison.Ordinal)).Tur);

    [Fact]
    public void Tevkifat_kodu_satira_gecer()
    {
        var xml = Ubl.Replace("<cac:Item><cbc:Name>Nakliye", "<cac:WithholdingTaxTotal><cac:TaxSubtotal><cac:TaxCategory><cac:TaxScheme><cbc:TaxTypeCode>601</cbc:TaxTypeCode></cac:TaxScheme></cac:TaxCategory></cac:TaxSubtotal></cac:WithholdingTaxTotal><cac:Item><cbc:Name>Nakliye", StringComparison.Ordinal);

        var s = Xml(xml).Satirlar[1];

        Assert.Equal("601", s.TevkifatKodu);
        Assert.True(s.TevkifatPayda > 0);
    }

    [Theory]
    [InlineData("bu xml degil")]
    [InlineData("<Order><ID>1</ID></Order>")]
    [InlineData("<Invoice><ID>1</ID></Invoice>")]
    [InlineData("<?xml version=\"1.0\"?><!DOCTYPE Invoice [<!ENTITY x \"y\">]><Invoice><ID>&x;</ID><IssueDate>2026-01-01</IssueDate></Invoice>")]
    public void Gecersiz_ya_da_dtdli_xml_reddedilir(string xml) =>
        Assert.False(UblOkuyucu.Oku(Encoding.UTF8.GetBytes(xml)).BasariliMi);

    [Fact]
    public void Satirsiz_fatura_reddedilir() =>
        Assert.Equal("belge_satir", UblOkuyucu.Oku(Encoding.UTF8.GetBytes("<Invoice><ID>A1</ID><IssueDate>2026-01-01</IssueDate></Invoice>")).Hata!.Kod);

    private const string Karekod = """{"vkntckn":"1234567890","avkntckn":"11111111110","senaryo":"TEMELFATURA","tip":"SATIS","tarih":"2026-10-15","no":"ABC2026000000042","ettn":"f47ac10b","parabirimi":"TRY","malhizmettoplam":"1500","kdvmatrah(20)":"1000.00","hesaplanankdv(20)":"200","kdvmatrah(10)":"500","hesaplanankdv(10)":"50","kdvmatrah(0)":"0","vergidahil":"1750","odenecek":"1750"}""";

    [Fact]
    public void Karekod_oran_basina_satir_uretir()
    {
        var b = FaturaKarekodu.Oku(Karekod).Deger;

        Assert.Equal((BelgeKaynagi.Karekod, "ABC2026000000042", "1234567890", "11111111110", 1750m), (b.Kaynak, b.Numara, b.TedarikciVkn!, b.AliciVkn!, b.Odenecek!.Value));
        Assert.Equal([(10, 500m), (20, 1000m)], b.Satirlar.Select(s => (s.KdvOrani, s.BirimFiyat)).OrderBy(x => x.KdvOrani));
        Assert.Contains(b.Uyarilar, u => u.Contains("satır", StringComparison.Ordinal));
    }

    [Fact]
    public void Karekod_virgullu_ve_buyuk_harfli_alan_okur()
    {
        var b = FaturaKarekodu.Oku("""{"VKNTCKN":"1234567890","TARIH":"15.10.2026","NO":"A1","KDVMATRAH(20)":"100,50","ODENECEK":"120,60"}""").Deger;

        Assert.Equal((new DateOnly(2026, 10, 15), 100.5m), (b.Tarih, b.Satirlar[0].BirimFiyat));
    }

    [Fact]
    public void Karekod_tevkifat_uyarisi()
    {
        var b = FaturaKarekodu.Oku(Karekod.Replace("\"vergidahil\"", "\"hesaplanantevkifatlikdv\":\"50\",\"vergidahil\"", StringComparison.Ordinal)).Deger;

        Assert.Contains(b.Uyarilar, u => u.Contains("tevkifat", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0102345678901234")]
    [InlineData("[1,2]")]
    [InlineData("""{"no":"A1"}""")]
    [InlineData("""{"vkntckn":"1","tarih":"bozuk","no":"A1","kdvmatrah(20)":"10"}""")]
    [InlineData("""{"vkntckn":"1","tarih":"2026-01-01","no":"A1"}""")]
    public void Fatura_karekodu_olmayan_reddedilir(string? metin) =>
        Assert.False(FaturaKarekodu.Oku(metin).BasariliMi);

    [Theory]
    [InlineData(1188, 1188.04, false)]
    [InlineData(1188, 1190, true)]
    public void Toplam_uyarisi(double belge, double hesap, bool uyarir) =>
        Assert.Equal(uyarir, BelgeKontrolu.ToplamUyarisi((decimal)belge, (decimal)hesap) is not null);

    [Fact]
    public void Baska_firmaya_kesilen_belge_uyarir()
    {
        Assert.NotNull(BelgeKontrolu.AliciUyarisi("111", "222"));
        Assert.Null(BelgeKontrolu.AliciUyarisi("222", "222"));
        Assert.Null(BelgeKontrolu.AliciUyarisi("222", null));
    }
}
