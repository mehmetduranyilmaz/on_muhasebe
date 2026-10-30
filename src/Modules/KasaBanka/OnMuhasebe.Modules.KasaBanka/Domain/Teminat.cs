using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

// Teminat takip defteri (nazım hesap): alınan/verilen teminatlar. Türe göre ek bilgiler (ipotekte ada/pafta/parsel, araç
// rehninde plaka/marka/model, mektupta komisyon...) tutulur. Kasa/muhasebe hareketi oluşturmaz.
internal sealed class Teminat : FirmaEntity
{
    private Teminat() { }

    public TeminatTuru Tur { get; private set; }
    public TeminatYonu Yon { get; private set; }

    // Lehtar/muhatap (verilen) ya da teminatı veren (alınan): cari hesap.
    public Guid CariHesapId { get; private set; }

    // Mektup/çek/nakit teminatta ilgili banka.
    public string? BankaAdi { get; private set; }

    // Mektup no / tapu yevmiye no / çek-senet no / sözleşme no (türüne göre).
    public string? ReferansNo { get; private set; }
    public decimal Tutar { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public DateOnly VerilisTarihi { get; private set; }
    public DateOnly? VadeTarihi { get; private set; }
    public DateOnly? IadeTarihi { get; private set; }
    public TeminatDurumu Durum { get; private set; }

    // Teminat mektubu komisyonu (bankaya ödenir).
    public decimal? KomisyonOrani { get; private set; }
    public decimal? KomisyonTutari { get; private set; }
    public KomisyonDonemi? KomisyonDonemi { get; private set; }

    // Türe özgü ek alanlar (TeminatSemasi'ndaki alan kodları -> değer). jsonb kolonunda tutulur.
    public Dictionary<string, string> Detaylar { get; private set; } = new();

    public string? Aciklama { get; private set; }

    public static Teminat Olustur(TeminatKaydetIstegi i) => new()
    {
        Tur = i.Tur,
        Yon = i.Yon,
        CariHesapId = i.CariHesapId,
        BankaAdi = TeminatSemasi.Bankali(i.Tur) ? Temiz(i.BankaAdi) : null,
        ReferansNo = Temiz(i.ReferansNo),
        Tutar = Para.Yuvarla(i.Tutar),
        ParaBirimi = i.ParaBirimi,
        VerilisTarihi = i.VerilisTarihi,
        VadeTarihi = i.VadeTarihi,
        Durum = TeminatDurumu.Aktif,
        KomisyonOrani = TeminatSemasi.Komisyonlu(i.Tur) ? i.KomisyonOrani : null,
        KomisyonTutari = TeminatSemasi.Komisyonlu(i.Tur) ? i.KomisyonTutari : null,
        KomisyonDonemi = TeminatSemasi.Komisyonlu(i.Tur) ? i.KomisyonDonemi : null,
        Detaylar = Suz(i.Tur, i.Detaylar),
        Aciklama = Temiz(i.Aciklama)
    };

    public void Guncelle(TeminatKaydetIstegi i)
    {
        Tur = i.Tur;
        Yon = i.Yon;
        CariHesapId = i.CariHesapId;
        BankaAdi = TeminatSemasi.Bankali(i.Tur) ? Temiz(i.BankaAdi) : null;
        ReferansNo = Temiz(i.ReferansNo);
        Tutar = Para.Yuvarla(i.Tutar);
        ParaBirimi = i.ParaBirimi;
        VerilisTarihi = i.VerilisTarihi;
        VadeTarihi = i.VadeTarihi;
        KomisyonOrani = TeminatSemasi.Komisyonlu(i.Tur) ? i.KomisyonOrani : null;
        KomisyonTutari = TeminatSemasi.Komisyonlu(i.Tur) ? i.KomisyonTutari : null;
        KomisyonDonemi = TeminatSemasi.Komisyonlu(i.Tur) ? i.KomisyonDonemi : null;
        Detaylar = Suz(i.Tur, i.Detaylar);
        Aciklama = Temiz(i.Aciklama);
    }

    // Aktif -> iade/irat (tarihli); irat/iade -> Aktif (tarih temizlenir).
    public void DurumDegistir(TeminatDurumu durum, DateOnly? tarih)
    {
        Durum = durum;
        IadeTarihi = durum == TeminatDurumu.Aktif ? null : tarih;
    }

    // Yalnızca türün şemasındaki alanları, boş olmayan değerlerle sakla.
    private static Dictionary<string, string> Suz(TeminatTuru tur, IReadOnlyDictionary<string, string>? detaylar)
    {
        var sonuc = new Dictionary<string, string>();
        if (detaylar is null)
        {
            return sonuc;
        }

        foreach (var alan in TeminatSemasi.Alanlar(tur))
        {
            if (detaylar.TryGetValue(alan.Kod, out var deger) && !string.IsNullOrWhiteSpace(deger))
            {
                sonuc[alan.Kod] = deger.Trim();
            }
        }

        return sonuc;
    }

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
