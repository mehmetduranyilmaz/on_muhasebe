using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Cari.Domain;

// Bir borç kaleminin (fatura, açılış, dekont...) bir alacak kalemiyle (tahsilat, iade...) kapatılan kısmı.
// Tutar carinin para birimindedir. Hareketlerden türetilmiş veridir: hareket iptal edilince kapamaları silinir.
internal sealed class CariKapama : FirmaEntity
{
    private CariKapama() { }

    public Guid CariHesapId { get; private set; }
    public Guid BorcHareketId { get; private set; }
    public Guid AlacakHareketId { get; private set; }
    public decimal Tutar { get; private set; }

    public static CariKapama Olustur(Guid cariHesapId, KapamaEslesmesi e) => new()
    {
        CariHesapId = cariHesapId,
        BorcHareketId = e.BorcId,
        AlacakHareketId = e.AlacakId,
        Tutar = e.Tutar
    };
}

// Kapanmamış kalem. Sira: kapama önceliği (vade, yoksa işlem tarihi).
internal sealed record KapamaKalemi(Guid Id, bool BorcMu, DateOnly Sira, DateOnly Tarih, decimal Kalan);

internal sealed record KapamaEslesmesi(Guid BorcId, Guid AlacakId, decimal Tutar);

// Açık kalem eşleştirme kuralları. Saf fonksiyonlardır; veritabanına dokunmaz.
internal static class CariKapamaHesaplayici
{
    // FIFO: en eski vadeli borç en eski vadeli alacakla kapatılır. Avans (fazla tahsilat) sonraki borçları kapatır.
    public static List<KapamaEslesmesi> Fifo(IEnumerable<KapamaKalemi> kalemler)
    {
        var liste = kalemler.Where(k => k.Kalan > 0).OrderBy(k => k.Sira).ThenBy(k => k.Tarih).ThenBy(k => k.Id).ToList();
        var borclar = new Queue<KapamaKalemi>(liste.Where(k => k.BorcMu));
        var alacaklar = new Queue<KapamaKalemi>(liste.Where(k => !k.BorcMu));
        var sonuc = new List<KapamaEslesmesi>();

        decimal borcKalan = 0, alacakKalan = 0;
        KapamaKalemi? borc = null, alacak = null;
        while (true)
        {
            if (borc is null || borcKalan == 0)
            {
                if (!borclar.TryDequeue(out borc))
                {
                    break;
                }

                borcKalan = borc.Kalan;
            }

            if (alacak is null || alacakKalan == 0)
            {
                if (!alacaklar.TryDequeue(out alacak))
                {
                    break;
                }

                alacakKalan = alacak.Kalan;
            }

            var tutar = Math.Min(borcKalan, alacakKalan);
            sonuc.Add(new KapamaEslesmesi(borc.Id, alacak.Id, tutar));
            borcKalan -= tutar;
            alacakKalan -= tutar;
        }

        return sonuc;
    }

    // Kalem önce belirtilen karşı kalemlere (ör. peşin tahsilat → kendi faturası) sırayla dağıtılır.
    public static List<KapamaEslesmesi> Oncelikli(KapamaKalemi kalem, IEnumerable<KapamaKalemi> hedefler)
    {
        var sonuc = new List<KapamaEslesmesi>();
        var kalan = kalem.Kalan;
        foreach (var hedef in hedefler.Where(h => h.BorcMu != kalem.BorcMu && h.Kalan > 0).OrderBy(h => h.Sira).ThenBy(h => h.Tarih))
        {
            if (kalan == 0)
            {
                break;
            }

            var tutar = Math.Min(kalan, hedef.Kalan);
            sonuc.Add(kalem.BorcMu ? new KapamaEslesmesi(kalem.Id, hedef.Id, tutar) : new KapamaEslesmesi(hedef.Id, kalem.Id, tutar));
            kalan -= tutar;
        }

        return sonuc;
    }

    // Eşleşmeler sonrası kalan tutarlar.
    public static List<KapamaKalemi> Dus(IEnumerable<KapamaKalemi> kalemler, IReadOnlyCollection<KapamaEslesmesi> eslesmeler) =>
        kalemler.Select(k => k with
        {
            Kalan = k.Kalan - eslesmeler.Where(e => e.BorcId == k.Id || e.AlacakId == k.Id).Sum(e => e.Tutar)
        }).ToList();
}
