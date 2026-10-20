using OnMuhasebe.Modules.Stok.Contracts;

namespace OnMuhasebe.Modules.Stok.Domain.Maliyet;

// Miktarlar temel birimde, fiyatlar TL/temel birim. Hareketler tarih sırasıyla verilir.
// Transferler ve iptal edilmiş hareket çiftleri firma geneli maliyeti etkilemediği için hesaplamaya verilmez.
internal readonly record struct MaliyetHareketi(Guid Id, decimal Giris, decimal Cikis, decimal? BirimFiyat);

internal sealed record MaliyetSonucu(
    decimal Miktar,
    decimal Deger,
    decimal BirimMaliyet,
    IReadOnlyDictionary<Guid, decimal> CikisBirimMaliyetleri);

internal interface IMaliyetHesaplayici
{
    MaliyetSonucu Hesapla(IEnumerable<MaliyetHareketi> hareketler);
}

internal static class MaliyetHesaplayicilari
{
    public static IMaliyetHesaplayici Getir(MaliyetYontemi yontem) => yontem switch
    {
        MaliyetYontemi.Fifo => new FifoHesaplayici(),
        MaliyetYontemi.SonAlisFiyati => new SonAlisFiyatiHesaplayici(),
        _ => new AgirlikliOrtalamaHesaplayici()
    };
}

// Hareketli ağırlıklı ortalama: her girişte ortalama yeniden hesaplanır, çıkışlar o anki ortalamayla maliyetlenir.
// Eksi bakiyeye giriş yapılırsa eksi kapanır, kalan miktar yeni giriş fiyatından değerlenir.
internal sealed class AgirlikliOrtalamaHesaplayici : IMaliyetHesaplayici
{
    public MaliyetSonucu Hesapla(IEnumerable<MaliyetHareketi> hareketler)
    {
        decimal miktar = 0, deger = 0, birimMaliyet = 0;
        var cikislar = new Dictionary<Guid, decimal>();

        foreach (var h in hareketler)
        {
            if (h.Giris > 0)
            {
                var fiyat = h.BirimFiyat ?? birimMaliyet;
                if (miktar < 0)
                {
                    miktar += h.Giris;
                    deger = miktar * fiyat;
                }
                else
                {
                    deger += h.Giris * fiyat;
                    miktar += h.Giris;
                }

                birimMaliyet = miktar > 0 ? deger / miktar : fiyat;
            }

            if (h.Cikis > 0)
            {
                cikislar[h.Id] = birimMaliyet;
                miktar -= h.Cikis;
                deger = miktar * birimMaliyet;
            }
        }

        return new MaliyetSonucu(miktar, deger, birimMaliyet, cikislar);
    }
}

// İlk giren ilk çıkar: girişler katman olarak tutulur, çıkışlar en eski katmandan düşülür.
// Katman kalmadığında eksiye düşen miktar son bilinen fiyattan maliyetlenir; sonraki giriş önce eksiyi kapatır.
internal sealed class FifoHesaplayici : IMaliyetHesaplayici
{
    public MaliyetSonucu Hesapla(IEnumerable<MaliyetHareketi> hareketler)
    {
        var katmanlar = new LinkedList<(decimal Miktar, decimal Fiyat)>();
        decimal eksi = 0, sonFiyat = 0;
        var cikislar = new Dictionary<Guid, decimal>();

        foreach (var h in hareketler)
        {
            if (h.Giris > 0)
            {
                var fiyat = h.BirimFiyat ?? sonFiyat;
                var kalan = h.Giris;
                var kapanan = Math.Min(eksi, kalan);
                eksi -= kapanan;
                kalan -= kapanan;
                if (kalan > 0)
                {
                    katmanlar.AddLast((kalan, fiyat));
                }

                sonFiyat = fiyat;
            }

            if (h.Cikis > 0)
            {
                var kalan = h.Cikis;
                decimal toplam = 0;
                while (kalan > 0 && katmanlar.First is { } ilk)
                {
                    var alinan = Math.Min(kalan, ilk.Value.Miktar);
                    toplam += alinan * ilk.Value.Fiyat;
                    kalan -= alinan;
                    if (alinan == ilk.Value.Miktar)
                    {
                        katmanlar.RemoveFirst();
                    }
                    else
                    {
                        ilk.Value = (ilk.Value.Miktar - alinan, ilk.Value.Fiyat);
                    }
                }

                if (kalan > 0)
                {
                    eksi += kalan;
                    toplam += kalan * sonFiyat;
                }

                cikislar[h.Id] = toplam / h.Cikis;
            }
        }

        var miktar = katmanlar.Sum(k => k.Miktar) - eksi;
        var deger = katmanlar.Sum(k => k.Miktar * k.Fiyat) - eksi * sonFiyat;
        return new MaliyetSonucu(miktar, deger, miktar != 0 ? deger / miktar : sonFiyat, cikislar);
    }
}

// Tüm stok ve çıkışlar o ana kadarki son giriş fiyatıyla değerlenir.
internal sealed class SonAlisFiyatiHesaplayici : IMaliyetHesaplayici
{
    public MaliyetSonucu Hesapla(IEnumerable<MaliyetHareketi> hareketler)
    {
        decimal miktar = 0, sonFiyat = 0;
        var cikislar = new Dictionary<Guid, decimal>();

        foreach (var h in hareketler)
        {
            if (h.Giris > 0)
            {
                sonFiyat = h.BirimFiyat ?? sonFiyat;
                miktar += h.Giris;
            }

            if (h.Cikis > 0)
            {
                cikislar[h.Id] = sonFiyat;
                miktar -= h.Cikis;
            }
        }

        return new MaliyetSonucu(miktar, miktar * sonFiyat, sonFiyat, cikislar);
    }
}
