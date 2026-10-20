using OnMuhasebe.Modules.Doviz.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

public class KurKurallariTestleri
{
    [Fact]
    public void Hafta_ici_islem_icin_once_onceki_gun_denenir()
    {
        // 21.10.2026 Çarşamba -> 20 (Salı), 19 (Pazartesi), 16 (Cuma)...
        var adaylar = KurKurallari.AdayBultenTarihleri(new DateOnly(2026, 10, 21)).Take(3);

        Assert.Equal([new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 16)], adaylar);
    }

    [Fact]
    public void Pazartesi_islemi_icin_cuma_bulteni_kullanilir_hafta_sonu_atlanir()
    {
        var adaylar = KurKurallari.AdayBultenTarihleri(new DateOnly(2026, 10, 19)).ToList();

        Assert.Equal(new DateOnly(2026, 10, 16), adaylar[0]);
        Assert.DoesNotContain(adaylar, t => t.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    [Fact]
    public void Geriye_en_fazla_on_gun_bakilir() =>
        Assert.All(KurKurallari.AdayBultenTarihleri(new DateOnly(2026, 10, 21)),
            t => Assert.True(t >= new DateOnly(2026, 10, 11)));

    [Fact]
    public void Tcmb_bulteni_birim_kura_cevrilir_bos_degerler_ve_xdr_atlanir()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Tarih_Date Tarih="20.10.2026" Date="10/20/2026">
              <Currency Kod="USD" CurrencyCode="USD"><Unit>1</Unit><ForexBuying>49.4889</ForexBuying><ForexSelling>49.5780</ForexSelling><BanknoteBuying>49.4542</BanknoteBuying><BanknoteSelling>49.6524</BanknoteSelling></Currency>
              <Currency Kod="JPY" CurrencyCode="JPY"><Unit>100</Unit><ForexBuying>32.4567</ForexBuying><ForexSelling>32.6712</ForexSelling><BanknoteBuying></BanknoteBuying><BanknoteSelling></BanknoteSelling></Currency>
              <Currency Kod="XDR" CurrencyCode="XDR"><Unit>1</Unit><ForexBuying>67.1234</ForexBuying><ForexSelling/><BanknoteBuying/><BanknoteSelling/></Currency>
            </Tarih_Date>
            """;

        var kurlar = KurKurallari.BulteniOku(xml);

        Assert.Equal(["USD", "JPY"], kurlar.Select(k => k.ParaBirimi));
        Assert.Equal(49.4889m, kurlar[0].DovizAlis);
        Assert.Equal(0.324567m, kurlar[1].DovizAlis);
        Assert.Null(kurlar[1].EfektifAlis);
    }
}
