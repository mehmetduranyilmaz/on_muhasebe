using System.Text.Json.Serialization;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<NakitPeriyodu>))]
public enum NakitPeriyodu
{
    Gun,
    Hafta,
    Ay
}

[JsonConverter(typeof(JsonStringEnumConverter<NakitKaynagi>))]
public enum NakitKaynagi
{
    CariTahsilat,
    CekSenetTahsil,
    PosAktarim,
    CariOdeme,
    CekSenetOdeme,
    KrediKartiOdeme
}

// Kalemin ekranda açılacağı kayıt.
[JsonConverter(typeof(JsonStringEnumConverter<NakitBaglantisi>))]
public enum NakitBaglantisi
{
    Cari,
    Fatura,
    Evrak,
    Hesap
}

public static class NakitKaynaklari
{
    public static bool GirisMi(NakitKaynagi kaynak) => kaynak is NakitKaynagi.CariTahsilat or NakitKaynagi.CekSenetTahsil or NakitKaynagi.PosAktarim;
}

// Beklenen tek bir giriş/çıkış. Tutar kendi dövizinde, TutarTl güncel kurla (TCMB döviz alış). GecikmisMi: beklenen tarih bugünden önce.
public sealed record NakitKalemi(
    DateOnly Tarih,
    NakitKaynagi Kaynak,
    string Aciklama,
    string? BelgeNo,
    string ParaBirimi,
    decimal Tutar,
    decimal TutarTl,
    bool GecikmisMi,
    NakitBaglantisi Baglanti,
    Guid BaglantiId);

public sealed record NakitDonemi(
    DateOnly Baslangic,
    DateOnly Bitis,
    decimal CariTahsilat,
    decimal CekSenetTahsil,
    decimal PosAktarim,
    decimal CariOdeme,
    decimal CekSenetOdeme,
    decimal KrediKartiOdeme,
    decimal Giris,
    decimal Cikis,
    decimal Net,
    decimal Kumulatif);

public sealed record HazirDeger(Guid HesapId, string Ad, HesapTuru Tur, string ParaBirimi, decimal Bakiye, decimal BakiyeTl);

// AcilisTl: kasa + banka bakiyeleri. IlkAcikDonem: kümülatif bakiyenin ilk eksiye düştüğü dönemin başı (nakit açığı).
// KurBulunamayanlar: TCMB kuru bulunamayan dövizler; bu dövizdeki kalemler 0 TL sayılmamak için rapora katılmaz.
public sealed record NakitAkisRaporu(
    DateOnly Tarih,
    NakitPeriyodu Periyot,
    bool GecikmisAlacakDahil,
    decimal AcilisTl,
    IReadOnlyList<HazirDeger> HazirDegerler,
    IReadOnlyList<NakitDonemi> Donemler,
    decimal GecikmisAlacakTl,
    decimal GecikmisBorcTl,
    decimal DonemSonuTl,
    decimal EnDusukBakiyeTl,
    DateOnly? IlkAcikDonem,
    IReadOnlyList<NakitKalemi> Kalemler,
    IReadOnlyList<string> KurBulunamayanlar);

// Nakit akışının dönemlere bölünmesi ve toplanması. Saf fonksiyonlardır; veritabanına dokunmaz.
// İhtiyatlılık: vadesi geçmiş borçlar ilk dönemde ödenecek sayılır; vadesi geçmiş alacaklar tahsil edilecek sayılmaz
// (istenirse ilk döneme dahil edilir).
public static class NakitAkisHesaplayici
{
    public static IReadOnlyList<(DateOnly Baslangic, DateOnly Bitis)> Donemler(DateOnly bugun, NakitPeriyodu periyot, int adet)
    {
        var sonuc = new List<(DateOnly, DateOnly)>(adet);
        var baslangic = bugun;
        for (var i = 0; i < adet; i++)
        {
            var bitis = periyot switch
            {
                NakitPeriyodu.Gun => baslangic,
                // Haftalar Pazartesi başlar (ISO 8601); ilk dönem bugünden pazara kadardır.
                NakitPeriyodu.Hafta => baslangic.AddDays(6 - ((int)baslangic.DayOfWeek + 6) % 7),
                _ => new DateOnly(baslangic.Year, baslangic.Month, DateTime.DaysInMonth(baslangic.Year, baslangic.Month))
            };
            sonuc.Add((baslangic, bitis));
            baslangic = bitis.AddDays(1);
        }

        return sonuc;
    }

    public static IReadOnlyList<NakitDonemi> Hesapla(
        decimal acilis, IEnumerable<NakitKalemi> kalemler, IReadOnlyList<(DateOnly Baslangic, DateOnly Bitis)> donemler, bool gecikmisAlacakDahil)
    {
        var tutarlar = new decimal[donemler.Count, 6];
        foreach (var k in kalemler)
        {
            if (k.GecikmisMi && NakitKaynaklari.GirisMi(k.Kaynak) && !gecikmisAlacakDahil)
            {
                continue;
            }

            var tarih = k.GecikmisMi ? donemler[0].Baslangic : k.Tarih;
            var i = DonemIndeksi(donemler, tarih);
            if (i >= 0)
            {
                tutarlar[i, (int)k.Kaynak] += k.TutarTl;
            }
        }

        var sonuc = new List<NakitDonemi>(donemler.Count);
        var kumulatif = acilis;
        for (var i = 0; i < donemler.Count; i++)
        {
            var giris = tutarlar[i, 0] + tutarlar[i, 1] + tutarlar[i, 2];
            var cikis = tutarlar[i, 3] + tutarlar[i, 4] + tutarlar[i, 5];
            kumulatif += giris - cikis;
            sonuc.Add(new NakitDonemi(donemler[i].Baslangic, donemler[i].Bitis,
                tutarlar[i, 0], tutarlar[i, 1], tutarlar[i, 2], tutarlar[i, 3], tutarlar[i, 4], tutarlar[i, 5],
                giris, cikis, giris - cikis, kumulatif));
        }

        return sonuc;
    }

    // Kredi kartı borcunun beklenen ödeme tarihi: bugünden sonraki ilk son ödeme günü (ay sonunu aşan gün ay sonuna çekilir).
    public static DateOnly SonrakiSonOdeme(DateOnly bugun, int sonOdemeGunu)
    {
        DateOnly Ayda(int yil, int ay) => new(yil, ay, Math.Min(sonOdemeGunu, DateTime.DaysInMonth(yil, ay)));
        var buAy = Ayda(bugun.Year, bugun.Month);
        if (buAy >= bugun)
        {
            return buAy;
        }

        var sonraki = bugun.AddMonths(1);
        return Ayda(sonraki.Year, sonraki.Month);
    }

    private static int DonemIndeksi(IReadOnlyList<(DateOnly Baslangic, DateOnly Bitis)> donemler, DateOnly tarih)
    {
        for (var i = 0; i < donemler.Count; i++)
        {
            if (tarih >= donemler[i].Baslangic && tarih <= donemler[i].Bitis)
            {
                return i;
            }
        }

        return -1;
    }
}
