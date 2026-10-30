using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

// Teminat takip defteri (nazım hesap): alınan/verilen teminatlar (teminat mektubu, ipotek, rehin, kefalet, nakit teminat...).
// Kasa ve muhasebe hareketi oluşturmaz; yalnızca kaydı ve durum takibini tutar.
internal sealed class Teminat : FirmaEntity
{
    private Teminat() { }

    public TeminatTuru Tur { get; private set; }
    public TeminatYonu Yon { get; private set; }
    public string TarafAdi { get; private set; } = null!;
    public string? ReferansNo { get; private set; }
    public decimal Tutar { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public DateOnly VerilisTarihi { get; private set; }
    public DateOnly? VadeTarihi { get; private set; }
    public DateOnly? IadeTarihi { get; private set; }
    public TeminatDurumu Durum { get; private set; }
    public string? Aciklama { get; private set; }

    public static Teminat Olustur(TeminatKaydetIstegi i) => new()
    {
        Tur = i.Tur,
        Yon = i.Yon,
        TarafAdi = i.TarafAdi.Trim(),
        ReferansNo = Temiz(i.ReferansNo),
        Tutar = Para.Yuvarla(i.Tutar),
        ParaBirimi = i.ParaBirimi,
        VerilisTarihi = i.VerilisTarihi,
        VadeTarihi = i.VadeTarihi,
        Durum = TeminatDurumu.Aktif,
        Aciklama = Temiz(i.Aciklama)
    };

    public void Guncelle(TeminatKaydetIstegi i)
    {
        Tur = i.Tur;
        Yon = i.Yon;
        TarafAdi = i.TarafAdi.Trim();
        ReferansNo = Temiz(i.ReferansNo);
        Tutar = Para.Yuvarla(i.Tutar);
        ParaBirimi = i.ParaBirimi;
        VerilisTarihi = i.VerilisTarihi;
        VadeTarihi = i.VadeTarihi;
        Aciklama = Temiz(i.Aciklama);
    }

    // Aktif -> iade/irat (tarihli); irat/iade -> Aktif (tarih temizlenir).
    public void DurumDegistir(TeminatDurumu durum, DateOnly? tarih)
    {
        Durum = durum;
        IadeTarihi = durum == TeminatDurumu.Aktif ? null : tarih;
    }

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
