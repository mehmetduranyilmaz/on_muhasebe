using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

internal static class DemirbasHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("demirbas_bulunamadi", "Demirbaş bulunamadı.");
    public static readonly Hata SicilKullaniliyor = Hata.Cakisma("demirbas_sicil", "Bu sicil numarası başka bir demirbaşta kullanılıyor.");
    public static readonly Hata ElCikarilmis = Hata.IsKurali("demirbas_el_cikarilmis", "Elden çıkarılmış demirbaş değiştirilemez; önce elden çıkarmayı geri alın.");
    public static readonly Hata ElCikarmaTarihi = Hata.Dogrulama("demirbas_cikis_tarihi", "Elden çıkarma tarihi edinme tarihinden önce olamaz.");
    public static readonly Hata ElCikarilmamis = Hata.IsKurali("demirbas_cikis_yok", "Demirbaş elden çıkarılmamış.");
    public static readonly Hata BinekTasitDegil = Hata.Dogrulama("demirbas_binek", "Binek otomobil yalnızca taşıtlar grubunda seçilebilir.");
}

// Sabit kıymet (amortismana tabi iktisadi kıymet). Amortisman planı her sorguda edinme bilgilerinden hesaplanır.
internal sealed class Demirbas : FirmaEntity, IIsyeriVarligi
{
    private Demirbas() { }

    public string SicilNo { get; private set; } = null!;
    public string Ad { get; private set; } = null!;
    public DemirbasGrubu Grup { get; private set; }
    public bool BinekOtomobilMi { get; private set; }
    public DateOnly EdinmeTarihi { get; private set; }
    public decimal EdinmeBedeli { get; private set; }
    public int FaydaliOmur { get; private set; }
    public AmortismanYontemi Yontem { get; private set; }
    public string? BelgeNo { get; private set; }
    public string? Zimmetli { get; private set; }
    public string? Konum { get; private set; }
    public Guid? MasrafMerkeziId { get; private set; }

    // Bulunduğu işyeri (envanter sayımı ve şube bazlı amortisman listesi için).
    public Guid IsyeriId { get; private set; }
    public string? Aciklama { get; private set; }

    public ElCikarmaTuru? ElCikarmaTuru { get; private set; }
    public DateOnly? ElCikarmaTarihi { get; private set; }
    public decimal? ElCikarmaBedeli { get; private set; }
    public string? ElCikarmaAciklamasi { get; private set; }

    public IReadOnlyList<AmortismanYili> Plan() =>
        AmortismanHesaplayici.Plan(EdinmeBedeli, EdinmeTarihi, FaydaliOmur, Yontem, BinekOtomobilMi, ElCikarmaTarihi);

    public static Sonuc<Demirbas> Olustur(string sicilNo, DemirbasKaydetIstegi istek, Guid isyeriId)
    {
        var demirbas = new Demirbas { SicilNo = sicilNo, IsyeriId = isyeriId };
        var sonuc = demirbas.Ayarla(sicilNo, istek);
        return sonuc.BasariliMi ? demirbas : sonuc.Hata;
    }

    public void IsyeriDegistir(Guid isyeriId) => IsyeriId = isyeriId;

    public Sonuc Guncelle(string sicilNo, DemirbasKaydetIstegi istek) =>
        ElCikarmaTarihi is not null ? DemirbasHatalari.ElCikarilmis : Ayarla(sicilNo, istek);

    public Sonuc ElCikar(ElCikarmaIstegi istek)
    {
        if (ElCikarmaTarihi is not null)
        {
            return DemirbasHatalari.ElCikarilmis;
        }

        if (istek.Tarih < EdinmeTarihi)
        {
            return DemirbasHatalari.ElCikarmaTarihi;
        }

        (ElCikarmaTuru, ElCikarmaTarihi, ElCikarmaBedeli) = (istek.Tur, istek.Tarih, istek.Tur == Contracts.ElCikarmaTuru.Hurda ? 0m : istek.Bedel);
        ElCikarmaAciklamasi = string.IsNullOrWhiteSpace(istek.Aciklama) ? null : istek.Aciklama.Trim();
        return Sonuc.Basarili();
    }

    public Sonuc ElCikarmayiGeriAl()
    {
        if (ElCikarmaTarihi is null)
        {
            return DemirbasHatalari.ElCikarilmamis;
        }

        (ElCikarmaTuru, ElCikarmaTarihi, ElCikarmaBedeli, ElCikarmaAciklamasi) = (null, null, null, null);
        return Sonuc.Basarili();
    }

    private Sonuc Ayarla(string sicilNo, DemirbasKaydetIstegi i)
    {
        if (i.BinekOtomobilMi && i.Grup != DemirbasGrubu.Tasitlar)
        {
            return DemirbasHatalari.BinekTasitDegil;
        }

        SicilNo = sicilNo;
        Ad = i.Ad.Trim();
        Grup = i.Grup;
        BinekOtomobilMi = i.BinekOtomobilMi;
        EdinmeTarihi = i.EdinmeTarihi;
        EdinmeBedeli = Math.Round(i.EdinmeBedeli, 2, MidpointRounding.AwayFromZero);
        FaydaliOmur = i.FaydaliOmur;
        Yontem = i.Yontem;
        BelgeNo = Temiz(i.BelgeNo);
        Zimmetli = Temiz(i.Zimmetli);
        Konum = Temiz(i.Konum);
        MasrafMerkeziId = i.MasrafMerkeziId;
        Aciklama = Temiz(i.Aciklama);
        return Sonuc.Basarili();
    }

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
