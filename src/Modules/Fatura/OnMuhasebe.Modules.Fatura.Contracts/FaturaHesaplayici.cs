namespace OnMuhasebe.Modules.Fatura.Contracts;

// TevkifatPay/TevkifatPayda (ör. 7/10) satırın hesaplanan KDV'sine uygulanır; 0 ise tevkifat yoktur.
public sealed record HesapSatiri(
    decimal Miktar,
    decimal BirimFiyat,
    bool KdvDahilMi,
    int KdvOrani,
    decimal Iskonto1,
    decimal Iskonto2,
    decimal Iskonto3,
    int TevkifatPay = 0,
    int TevkifatPayda = 0);

// Tutarlar KDV hariçtir: Brut - Iskonto = net; net - GenelIskontoPayi = Matrah; Matrah + Kdv = Toplam.
// TevkifEdilenKdv, KDV'nin alıcı tarafından satıcıya ödenmeyip vergi dairesine yatırılan kısmıdır.
public sealed record SatirHesabi(
    decimal BrutTutar,
    decimal IskontoTutari,
    decimal GenelIskontoPayi,
    decimal Matrah,
    decimal KdvTutari,
    decimal Toplam,
    decimal TevkifEdilenKdv = 0m);

public sealed record KdvDagilimi(int Oran, decimal Matrah, decimal KdvTutari);

// GenelToplam belgenin brütüdür (Matrah + KDV) ve tevkifat/stopajla değişmez.
// OdenecekTutar carinin gördüğü/tahsil-ödeme yapılan tutardır: GenelToplam - tevkif edilen KDV - stopaj.
public sealed record FaturaHesabi(
    IReadOnlyList<SatirHesabi> Satirlar,
    decimal BrutToplam,
    decimal SatirIskontoToplami,
    decimal GenelIskontoTutari,
    decimal Matrah,
    decimal KdvToplami,
    decimal GenelToplam,
    IReadOnlyList<KdvDagilimi> KdvDagilimi,
    decimal TevkifEdilenKdvToplami,
    decimal StopajTutari,
    decimal OdenecekTutar);

// Fatura tutarlarının tek hesap noktası. API kaydederken, Web ekranda canlı gösterirken aynı kodu kullanır.
// Kurallar: satır iskontoları zincirleme uygulanır (%10+%5 = %14,5); genel iskonto KDV matrahından düşülmesi için
// satırlara net tutarları oranında dağıtılır, kuruş farkı en büyük satıra yazılır; tutarlar 2 haneye yukarı yuvarlanır.
// Stopaj (gelir vergisi tevkifatı) gider pusulası/serbest meslek makbuzunda matrah toplamına uygulanır.
public static class FaturaHesaplayici
{
    public static FaturaHesabi Hesapla(
        IReadOnlyList<HesapSatiri> satirlar, GenelIskontoTuru genelIskontoTuru, decimal genelIskonto, decimal stopajOrani = 0m)
    {
        var ara = satirlar.Select(AraHesap).ToList();
        var netToplam = ara.Sum(a => a.Net);
        var paylar = GenelIskontoPaylari(ara.Select(a => a.Net).ToList(), GenelIskontoTutari(netToplam, genelIskontoTuru, genelIskonto));

        var sonuclar = new List<SatirHesabi>(ara.Count);
        for (var i = 0; i < ara.Count; i++)
        {
            var a = ara[i];
            var matrah = a.Net - paylar[i];

            // KDV dahil fiyatta (genel iskonto yoksa) KDV, dahil tutardan matrah çıkarılarak bulunur;
            // böylece raf fiyatı 99,99 ise fatura toplamı da kuruşu kuruşuna 99,99 olur.
            var kdv = a.DahilNet is { } dahilNet && paylar[i] == 0m
                ? dahilNet - matrah
                : Yuvarla(matrah * satirlar[i].KdvOrani / 100m);
            var tevkif = TevkifEdilenKdv(kdv, satirlar[i].TevkifatPay, satirlar[i].TevkifatPayda);
            sonuclar.Add(new SatirHesabi(a.Brut, a.Brut - a.Net, paylar[i], matrah, kdv, matrah + kdv, tevkif));
        }

        var dagilim = satirlar.Zip(sonuclar)
            .GroupBy(x => x.First.KdvOrani)
            .OrderBy(g => g.Key)
            .Select(g => new KdvDagilimi(g.Key, g.Sum(x => x.Second.Matrah), g.Sum(x => x.Second.KdvTutari)))
            .ToList();

        var matrahToplam = sonuclar.Sum(s => s.Matrah);
        var kdvToplam = sonuclar.Sum(s => s.KdvTutari);
        var genelToplam = sonuclar.Sum(s => s.Toplam);
        var tevkifToplam = sonuclar.Sum(s => s.TevkifEdilenKdv);
        var stopaj = stopajOrani > 0m ? Yuvarla(matrahToplam * stopajOrani / 100m) : 0m;

        return new FaturaHesabi(
            sonuclar,
            sonuclar.Sum(s => s.BrutTutar),
            sonuclar.Sum(s => s.IskontoTutari),
            sonuclar.Sum(s => s.GenelIskontoPayi),
            matrahToplam,
            kdvToplam,
            genelToplam,
            dagilim,
            tevkifToplam,
            stopaj,
            genelToplam - tevkifToplam - stopaj);
    }

    public static decimal Yuvarla(decimal tutar) => Math.Round(tutar, 2, MidpointRounding.AwayFromZero);

    public static decimal ZincirCarpani(decimal iskonto1, decimal iskonto2, decimal iskonto3) =>
        (1m - iskonto1 / 100m) * (1m - iskonto2 / 100m) * (1m - iskonto3 / 100m);

    private static decimal TevkifEdilenKdv(decimal kdv, int pay, int payda) =>
        payda > 0 && pay > 0 ? Yuvarla(kdv * pay / payda) : 0m;

    private static (decimal Brut, decimal Net, decimal? DahilNet) AraHesap(HesapSatiri s)
    {
        var carpan = ZincirCarpani(s.Iskonto1, s.Iskonto2, s.Iskonto3);
        if (!s.KdvDahilMi)
        {
            var brut = Yuvarla(s.Miktar * s.BirimFiyat);
            return (brut, Yuvarla(brut * carpan), null);
        }

        var kdvCarpani = 1m + s.KdvOrani / 100m;
        var dahilBrut = Yuvarla(s.Miktar * s.BirimFiyat);
        var dahilNet = Yuvarla(dahilBrut * carpan);
        return (Yuvarla(dahilBrut / kdvCarpani), Yuvarla(dahilNet / kdvCarpani), dahilNet);
    }

    // Tutar olarak girilen genel iskonto net toplamı aşamaz.
    private static decimal GenelIskontoTutari(decimal netToplam, GenelIskontoTuru tur, decimal deger)
    {
        if (deger <= 0m || netToplam <= 0m)
        {
            return 0m;
        }

        var tutar = tur == GenelIskontoTuru.Yuzde ? Yuvarla(netToplam * deger / 100m) : Yuvarla(deger);
        return Math.Min(tutar, netToplam);
    }

    private static decimal[] GenelIskontoPaylari(List<decimal> netler, decimal toplamIskonto)
    {
        var paylar = new decimal[netler.Count];
        var netToplam = netler.Sum();
        if (toplamIskonto == 0m || netToplam == 0m)
        {
            return paylar;
        }

        for (var i = 0; i < netler.Count; i++)
        {
            paylar[i] = Yuvarla(toplamIskonto * netler[i] / netToplam);
        }

        var enBuyuk = netler.IndexOf(netler.Max());
        paylar[enBuyuk] += toplamIskonto - paylar.Sum();
        return paylar;
    }
}
