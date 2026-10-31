namespace OnMuhasebe.Modules.Cari.Contracts;

// Bir borç kaleminin kapandığı ödeme (tahsilat tarihi; çek/senetle ödemede evrakın vadesi, çünkü para o gün tahsil edilir).
public sealed record VadeFarkiOdemesi(DateOnly Tarih, decimal Tutar);

public sealed record VadeFarkiGirdisi(
    Guid HareketId,
    CariHareketTuru Tur,
    string? BelgeNo,
    DateOnly Vade,
    decimal Tutar,
    IReadOnlyList<VadeFarkiOdemesi> Odemeler,
    decimal Kalan,
    Guid? KaynakBelgeId);

// OdemeTarihi null ise kısım hâlâ açıktır (faiz hesap tarihine kadar işler).
public sealed record VadeFarkiSatiri(
    Guid HareketId,
    CariHareketTuru Tur,
    string? BelgeNo,
    DateOnly Vade,
    DateOnly? OdemeTarihi,
    decimal GecikenTutar,
    int Gun,
    decimal Faiz,
    Guid? KaynakBelgeId);

public sealed record VadeFarkiHesabi(
    Guid CariHesapId,
    string ParaBirimi,
    DateOnly? Baslangic,
    DateOnly Bitis,
    decimal YillikOran,
    int ToleransGun,
    IReadOnlyList<VadeFarkiSatiri> Satirlar,
    decimal ToplamFaiz);

// Vade farkı (gecikme faizi): basit faiz, 365 gün esası (ticari işlerde yıllık oran). Gecikme tolerans gününü aşmazsa faiz işlemez;
// aşarsa faiz vadeden itibaren işler. Başlangıç verilirse (son vade farkı faturası tarihi) faiz o tarihten önceye gitmez; böylece
// aynı gecikme iki kez faturalanmaz. Geç ödenen kısım ödeme tarihine, açık kısım hesap tarihine kadar faizlenir.
public static class VadeFarkiHesaplayici
{
    public const int YilGun = 365;

    public static IReadOnlyList<VadeFarkiSatiri> Hesapla(
        IEnumerable<VadeFarkiGirdisi> kalemler, decimal yillikOran, DateOnly? baslangic, DateOnly bitis, int toleransGun)
    {
        var sonuc = new List<VadeFarkiSatiri>();
        foreach (var k in kalemler)
        {
            var faizBaslangici = baslangic is { } b && b > k.Vade ? b : k.Vade;
            foreach (var o in k.Odemeler)
            {
                Ekle(k, o.Tarih, o.Tutar);
            }

            if (k.Kalan > 0)
            {
                Ekle(k, null, k.Kalan);
            }

            void Ekle(VadeFarkiGirdisi kalem, DateOnly? odeme, decimal tutar)
            {
                var son = odeme is { } t && t < bitis ? t : bitis;
                var gecikme = son.DayNumber - kalem.Vade.DayNumber;
                var gun = son.DayNumber - faizBaslangici.DayNumber;
                if (tutar <= 0 || gecikme <= toleransGun || gun <= 0)
                {
                    return;
                }

                var faiz = Math.Round(tutar * yillikOran / 100m * gun / YilGun, 2, MidpointRounding.AwayFromZero);
                if (faiz > 0)
                {
                    sonuc.Add(new VadeFarkiSatiri(kalem.HareketId, kalem.Tur, kalem.BelgeNo, kalem.Vade, odeme is { } od && od <= bitis ? od : null,
                        tutar, gun, faiz, kalem.KaynakBelgeId));
                }
            }
        }

        return sonuc;
    }
}
