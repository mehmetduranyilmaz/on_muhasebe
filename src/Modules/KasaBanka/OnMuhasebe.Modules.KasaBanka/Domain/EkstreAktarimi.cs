using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain.Ekstre;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.KasaBanka.Domain;

// Bir ekstre dosyasının yüklenip aktarılma süreci. Dosya içeriği aktarım tamamlanana kadar saklanır (eşleme değişirse
// yeniden çözülebilsin diye), sonra silinir.
internal sealed class EkstreAktarimi : FirmaEntity
{
    public const int AzamiDosyaBoyutu = 5 * 1024 * 1024;

    private readonly List<EkstreSatiri> _satirlar = [];

    private EkstreAktarimi() { }

    public Guid HesapId { get; private set; }
    public string DosyaAdi { get; private set; } = null!;
    public EkstreBicimi Bicim { get; private set; }
    public AktarimDurumu Durum { get; private set; }
    public byte[]? Icerik { get; private set; }
    public KolonEslemesi? Esleme { get; private set; }
    public DateOnly? Baslangic { get; private set; }
    public DateOnly? Bitis { get; private set; }
    public string? BakiyeUyarisi { get; private set; }
    public DateTime? TamamlanmaTarihi { get; private set; }

    public IReadOnlyCollection<EkstreSatiri> Satirlar => _satirlar;

    public static EkstreAktarimi Olustur(Guid hesapId, string dosyaAdi, EkstreBicimi bicim, byte[] icerik) => new()
    {
        HesapId = hesapId,
        DosyaAdi = dosyaAdi,
        Bicim = bicim,
        Icerik = icerik,
        Durum = AktarimDurumu.EslemeBekliyor
    };

    // Çözülmüş satırlar yüklenir; varsa önceki (aktarılmamış) satırların yerini alır.
    public void SatirlariYukle(KolonEslemesi? esleme, IReadOnlyList<EkstreSatiri> satirlar, string? bakiyeUyarisi)
    {
        Esleme = esleme;
        _satirlar.Clear();
        _satirlar.AddRange(satirlar);
        Baslangic = satirlar.Count == 0 ? null : satirlar.Min(s => s.Tarih);
        Bitis = satirlar.Count == 0 ? null : satirlar.Max(s => s.Tarih);
        BakiyeUyarisi = bakiyeUyarisi;
        Durum = AktarimDurumu.OnayBekliyor;
    }

    // Bekleyen ya da hatalı satır kalmadıysa aktarım tamamlanır ve dosya içeriği silinir.
    public void TamamlandiysaKapat(DateTime simdi)
    {
        if (_satirlar.Any(s => s.Durum is EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali))
        {
            return;
        }

        Durum = AktarimDurumu.Tamamlandi;
        TamamlanmaTarihi = simdi;
        Icerik = null;
    }
}

internal sealed class EkstreSatiri : BaseEntity
{
    private EkstreSatiri() { }

    public Guid AktarimId { get; private set; }
    public Guid HesapId { get; private set; }
    public int Sira { get; private set; }
    public DateOnly Tarih { get; private set; }
    public string Aciklama { get; private set; } = null!;
    public decimal Tutar { get; private set; }
    public decimal? Bakiye { get; private set; }
    public string? Referans { get; private set; }
    public string ParmakIzi { get; private set; } = null!;
    public EkstreSatirDurumu Durum { get; private set; }
    public EkstreIslemTuru IslemTuru { get; private set; }
    public Guid? CariHesapId { get; private set; }
    public Guid? KategoriId { get; private set; }
    public Guid? KarsiHesapId { get; private set; }
    public Guid? EslesenHareketId { get; private set; }
    public Guid? IslemId { get; private set; }
    public Guid? KuralId { get; private set; }
    public string? OneriKaynagi { get; private set; }
    public string? Hata { get; private set; }
    public bool ElleDuzenlendi { get; private set; }

    public bool GirisMi => Tutar > 0;

    public static EkstreSatiri Olustur(Guid aktarimId, Guid hesapId, int sira, CozulmusSatir s, string parmakIzi) => new()
    {
        AktarimId = aktarimId,
        HesapId = hesapId,
        Sira = sira,
        Tarih = s.Tarih,
        Aciklama = s.Aciklama.Length > 500 ? s.Aciklama[..500] : s.Aciklama,
        Tutar = s.Tutar,
        Bakiye = s.Bakiye,
        Referans = s.Referans is { Length: > 50 } r ? r[..50] : s.Referans,
        ParmakIzi = parmakIzi,
        Durum = EkstreSatirDurumu.Bekliyor
    };

    public void OneriUygula(Oneri o)
    {
        (IslemTuru, CariHesapId, KategoriId, KarsiHesapId, KuralId, OneriKaynagi) =
            (o.IslemTuru, o.CariHesapId, o.KategoriId, o.KarsiHesapId, o.KuralId, o.Kaynak);
    }

    public void DahaOnceAktarildiIsaretle() => Durum = EkstreSatirDurumu.DahaOnceAktarildi;

    public void EslesmeAta(Guid hareketId)
    {
        EslesenHareketId = hareketId;
        Durum = EkstreSatirDurumu.ZatenKayitli;
    }

    public void Duzenle(EkstreIslemTuru tur, Guid? cariId, Guid? kategoriId, Guid? karsiHesapId, bool eslesmeyiKaldir)
    {
        (IslemTuru, CariHesapId, KategoriId, KarsiHesapId) = (tur, cariId, kategoriId, karsiHesapId);
        if (eslesmeyiKaldir && Durum == EkstreSatirDurumu.ZatenKayitli)
        {
            EslesenHareketId = null;
            Durum = EkstreSatirDurumu.Bekliyor;
        }

        if (Durum == EkstreSatirDurumu.Hatali)
        {
            (Durum, Hata) = (EkstreSatirDurumu.Bekliyor, null);
        }

        (KuralId, OneriKaynagi, ElleDuzenlendi) = (null, "Elle", true);
    }

    public void Aktarildi(Guid islemId) => (IslemId, Durum, Hata) = (islemId, EkstreSatirDurumu.Aktarildi, null);

    public void Atlandi() => Durum = EkstreSatirDurumu.Atlandi;

    public void Basarisiz(string hata) => (Durum, Hata) = (EkstreSatirDurumu.Hatali, hata);
}

// Hesap başına kolon eşleme şablonu; aynı bankanın sonraki ekstrelerinde eşleme sorulmaz.
internal sealed class EkstreSablonu : FirmaEntity
{
    private EkstreSablonu() { }

    public Guid HesapId { get; private set; }
    public KolonEslemesi Esleme { get; private set; } = null!;

    public static EkstreSablonu Olustur(Guid hesapId, KolonEslemesi esleme) => new() { HesapId = hesapId, Esleme = esleme };

    public void Guncelle(KolonEslemesi esleme) => Esleme = esleme;
}

// Açıklamada anahtar kelime geçen satırlar için öğrenilmiş öneri. Anahtar kelime karşılaştırma biçiminde (büyük harf, ASCII) tutulur.
internal sealed class EkstreKurali : FirmaEntity
{
    private EkstreKurali() { }

    public string AnahtarKelime { get; private set; } = null!;
    public KuralYonu Yon { get; private set; }
    public EkstreIslemTuru IslemTuru { get; private set; }
    public Guid? CariHesapId { get; private set; }
    public Guid? KategoriId { get; private set; }
    public Guid? KarsiHesapId { get; private set; }
    public int KullanimSayisi { get; private set; }
    public DateTime? SonKullanim { get; private set; }

    public static EkstreKurali Olustur(string anahtar, KuralYonu yon) => new() { AnahtarKelime = EkstreMetni.Normalize(anahtar), Yon = yon };

    public void Ayarla(EkstreIslemTuru tur, Guid? cariId, Guid? kategoriId, Guid? karsiHesapId) =>
        (IslemTuru, CariHesapId, KategoriId, KarsiHesapId) = (tur, cariId, kategoriId, karsiHesapId);

    public void AnahtarDegistir(string anahtar, KuralYonu yon) => (AnahtarKelime, Yon) = (EkstreMetni.Normalize(anahtar), yon);

    public void Kullanildi(DateTime simdi)
    {
        KullanimSayisi++;
        SonKullanim = simdi;
    }

    public KuralVerisi Veri() => new(Id, AnahtarKelime, Yon, IslemTuru, CariHesapId, KategoriId, KarsiHesapId);
}
