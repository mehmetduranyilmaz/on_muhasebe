using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Domain;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

// Çek ya da senet. Durumu yalnızca hareketlerle değişir; her hareket önceki durumu saklar ve son hareket geri alınabilir.
internal sealed class Evrak : FirmaEntity
{
    private readonly List<EvrakHareketi> _hareketler = [];

    private Evrak() { }

    // Portföy numarası (AC/AS/VC/VS + sıra).
    public string Kod { get; private set; } = null!;
    public EvrakTuru Tur { get; private set; }
    public EvrakYonu Yon { get; private set; }
    public string SeriNo { get; private set; } = null!;
    public DateOnly GirisTarihi { get; private set; }
    public DateOnly VadeTarihi { get; private set; }
    public decimal Tutar { get; private set; }
    public string ParaBirimi { get; private set; } = null!;

    // Giriş günündeki kur; TL karşılıklı raporlar (vade takvimi, portföy toplamı) bununla hesaplanır.
    public decimal Kur { get; private set; }

    // Alınan evrakta getiren müşteri, verilende tedarikçi. CariTutar carinin dövizinde; ters işlemler (iade, karşılıksız) aynı tutarla yazılır.
    public Guid CariHesapId { get; private set; }
    public decimal CariTutar { get; private set; }
    public decimal CariKur { get; private set; }

    public Guid? BorcluId { get; private set; }
    public string? BorcluAdi { get; private set; }
    public string? BorcluKimlikNo { get; private set; }
    public string? BankaAdi { get; private set; }
    public string? Sube { get; private set; }
    public string? HesapNo { get; private set; }
    public string? KesideYeri { get; private set; }
    public string? KefilAdi { get; private set; }
    public string? KefilKimlikNo { get; private set; }

    // Verilen çekin bağlı olduğu banka hesabı.
    public Guid? KendiHesapId { get; private set; }

    public EvrakDurumu Durum { get; private set; }

    // Tahsile ya da teminata verildiği banka.
    public Guid? KonumHesapId { get; private set; }

    // Ciro edildiği cari ve o carinin dövizindeki tutar.
    public Guid? CiroCariId { get; private set; }
    public decimal? CiroCariTutar { get; private set; }
    public decimal? CiroCariKur { get; private set; }

    // Karşılıksızdan sonra tahsil edilen kısım (evrak dövizinde).
    public decimal TahsilEdilen { get; private set; }
    public DateOnly? KarsiliksizTarihi { get; private set; }

    // Riskli keşideci/borçlu uyarısına rağmen kabul edildiyse onaylayan ve zamanı.
    public Guid? RiskOnaylayanId { get; private set; }
    public DateTime? RiskOnayTarihi { get; private set; }
    public string? Aciklama { get; private set; }

    public IReadOnlyList<EvrakHareketi> Hareketler => _hareketler;

    public decimal KalanTutar => Tutar - TahsilEdilen;

    public static string KodOneki(EvrakYonu yon, EvrakTuru tur) => (yon, tur) switch
    {
        (EvrakYonu.Alinan, EvrakTuru.Cek) => "AC",
        (EvrakYonu.Alinan, EvrakTuru.Senet) => "AS",
        (EvrakYonu.Verilen, EvrakTuru.Cek) => "VC",
        _ => "VS"
    };

    public static Evrak Alinan(
        string kod, AlinanEvrakGirdisi g, DateOnly tarih, Guid cariId, decimal kur, decimal cariTutar, decimal cariKur,
        EvrakBorclusu? borclu, string? borcluAdi, string? borcluKimlikNo) => new()
    {
        Kod = kod,
        Tur = g.Tur,
        Yon = EvrakYonu.Alinan,
        SeriNo = g.SeriNo.Trim(),
        GirisTarihi = tarih,
        VadeTarihi = g.VadeTarihi,
        Tutar = Para.Yuvarla(g.Tutar),
        ParaBirimi = g.ParaBirimi,
        Kur = kur,
        CariHesapId = cariId,
        CariTutar = cariTutar,
        CariKur = cariKur,
        BorcluId = borclu?.Id,
        BorcluAdi = borclu?.Ad ?? borcluAdi,
        BorcluKimlikNo = borclu?.KimlikNo ?? borcluKimlikNo,
        BankaAdi = Temiz(g.BankaAdi),
        Sube = Temiz(g.Sube),
        HesapNo = Temiz(g.HesapNo),
        KesideYeri = Temiz(g.KesideYeri),
        KefilAdi = Temiz(g.KefilAdi),
        KefilKimlikNo = Temiz(g.KefilKimlikNo),
        Durum = EvrakDurumu.Portfoyde,
        Aciklama = Temiz(g.Aciklama)
    };

    public static Evrak Verilen(
        string kod, VerilenEvrakGirdisi g, string paraBirimi, DateOnly tarih, Guid cariId, decimal kur, decimal cariTutar, decimal cariKur,
        KasaHesabi? kendiHesap) => new()
    {
        Kod = kod,
        Tur = g.Tur,
        Yon = EvrakYonu.Verilen,
        SeriNo = g.SeriNo.Trim(),
        GirisTarihi = tarih,
        VadeTarihi = g.VadeTarihi,
        Tutar = Para.Yuvarla(g.Tutar),
        ParaBirimi = paraBirimi,
        Kur = kur,
        CariHesapId = cariId,
        CariTutar = cariTutar,
        CariKur = cariKur,
        KendiHesapId = kendiHesap?.Id,
        BankaAdi = kendiHesap?.BankaAdi,
        HesapNo = kendiHesap?.HesapNo,
        Durum = EvrakDurumu.Verildi,
        Aciklama = Temiz(g.Aciklama)
    };

    public void RiskOnayla(Guid kullaniciId, DateTime zaman)
    {
        RiskOnaylayanId = kullaniciId;
        RiskOnayTarihi = zaman;
    }

    public EvrakHareketi Ilk(Guid bordroId, Guid islemId, DateOnly tarih, string? aciklama)
    {
        var hareket = EvrakHareketi.Olustur(this, bordroId, islemId, Yon == EvrakYonu.Alinan ? EvrakIslemi.Giris : EvrakIslemi.Cikis,
            tarih, null, Durum, null, CariHesapId, Tutar, aciklama);
        _hareketler.Add(hareket);
        return hareket;
    }

    // Geçişi uygular. hesapId: tahsil/teminat/ödeme hesabı; ciroCari: ciro edilen cari ve tutarı; tutar: karşılıksızdan kısmi tahsil.
    public Sonuc<EvrakHareketi> Uygula(
        Gecis gecis, Guid bordroId, Guid islemId, DateOnly tarih, Guid? hesapId, (Guid Id, decimal Tutar, decimal Kur)? ciroCari, decimal? tutar,
        string? aciklama)
    {
        if (gecis.Yon != Yon || !gecis.Kaynaklar.Contains(Durum))
        {
            return KasaBankaHatalari.EvrakIslemiGecersiz;
        }

        if (tarih < SonHareketTarihi)
        {
            return KasaBankaHatalari.EvrakTarihiGeride;
        }

        var islemTutari = Tutar;
        var hedef = gecis.Hedef;
        if (gecis.Islem == EvrakIslemi.KarsiliksizdanTahsil)
        {
            islemTutari = Para.Yuvarla(tutar ?? KalanTutar);
            if (islemTutari <= 0 || islemTutari > KalanTutar)
            {
                return KasaBankaHatalari.EvrakTahsilTutari;
            }

            hedef = islemTutari == KalanTutar ? EvrakDurumu.TahsilEdildi : Durum;
        }

        var hareket = EvrakHareketi.Olustur(this, bordroId, islemId, gecis.Islem, tarih, Durum, hedef, hesapId, ciroCari?.Id ?? CiroCariId ?? CariHesapId,
            islemTutari, aciklama);
        hareket.OncekileriSakla(KonumHesapId, CiroCariId, CiroCariTutar, CiroCariKur, KarsiliksizTarihi);

        switch (gecis.Islem)
        {
            case EvrakIslemi.TahsileVerme or EvrakIslemi.TeminataVerme:
                KonumHesapId = hesapId;
                break;
            case EvrakIslemi.TahsildenGeriAlma or EvrakIslemi.TeminattanAlma or EvrakIslemi.Tahsil:
                KonumHesapId = null;
                break;
            case EvrakIslemi.Ciro:
                (CiroCariId, CiroCariTutar, CiroCariKur) = (ciroCari!.Value.Id, ciroCari.Value.Tutar, ciroCari.Value.Kur);
                break;
            case EvrakIslemi.CirodanGeriAlma:
                (CiroCariId, CiroCariTutar, CiroCariKur) = (null, null, null);
                break;
            case EvrakIslemi.CirodanKarsiliksiz or EvrakIslemi.Karsiliksiz or EvrakIslemi.VerilenKarsiliksiz:
                (CiroCariId, CiroCariTutar, CiroCariKur) = (null, null, null);
                KonumHesapId = null;
                KarsiliksizTarihi = tarih;
                break;
            case EvrakIslemi.KarsiliksizdanTahsil:
                TahsilEdilen += islemTutari;
                break;
        }

        Durum = hedef;
        _hareketler.Add(hareket);
        return hareket;
    }

    public EvrakHareketi? GeriAlinacak =>
        _hareketler.Where(h => !h.GeriAlindi).OrderByDescending(h => h.Sira).FirstOrDefault();

    // Son hareketi geri alır; giriş/çıkış hareketi geri alınırsa evrak iptal edilir.
    public Sonuc<EvrakHareketi> SonHareketiGeriAl(DateTime zaman)
    {
        if (GeriAlinacak is not { } son || Durum == EvrakDurumu.Iptal)
        {
            return KasaBankaHatalari.EvrakGeriAlinamaz;
        }

        son.GeriAl(zaman);
        if (son.Islem is EvrakIslemi.Giris or EvrakIslemi.Cikis)
        {
            Durum = EvrakDurumu.Iptal;
            return son;
        }

        Durum = son.OncekiDurum!.Value;
        KonumHesapId = son.OncekiKonumHesapId;
        (CiroCariId, CiroCariTutar, CiroCariKur) = (son.OncekiCiroCariId, son.OncekiCiroCariTutar, son.OncekiCiroCariKur);
        KarsiliksizTarihi = son.OncekiKarsiliksizTarihi;
        if (son.Islem == EvrakIslemi.KarsiliksizdanTahsil)
        {
            TahsilEdilen -= son.Tutar;
        }

        return son;
    }

    private DateOnly SonHareketTarihi => _hareketler.Where(h => !h.GeriAlindi).Select(h => h.Tarih).DefaultIfEmpty(GirisTarihi).Max();

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

internal sealed class EvrakHareketi : FirmaEntity, IDonemeTabi
{
    private EvrakHareketi() { }

    public Guid EvrakId { get; private set; }
    public Guid BordroId { get; private set; }

    // Hareketin kasa ve cari kayıtlarının ortak kimliği (cari hareketinin kaynak belgesi); geri almada bununla bulunur.
    public Guid IslemId { get; private set; }
    public int Sira { get; private set; }
    public EvrakIslemi Islem { get; private set; }
    public DateOnly Tarih { get; private set; }
    public EvrakDurumu? OncekiDurum { get; private set; }
    public EvrakDurumu YeniDurum { get; private set; }
    public Guid? HesapId { get; private set; }
    public Guid? CariHesapId { get; private set; }
    public decimal Tutar { get; private set; }
    public string? Aciklama { get; private set; }
    public bool GeriAlindi { get; private set; }
    public DateTime? GeriAlmaTarihi { get; private set; }

    public Guid? OncekiKonumHesapId { get; private set; }
    public Guid? OncekiCiroCariId { get; private set; }
    public decimal? OncekiCiroCariTutar { get; private set; }
    public decimal? OncekiCiroCariKur { get; private set; }
    public DateOnly? OncekiKarsiliksizTarihi { get; private set; }

    public static EvrakHareketi Olustur(
        Evrak evrak, Guid bordroId, Guid islemId, EvrakIslemi islem, DateOnly tarih, EvrakDurumu? onceki, EvrakDurumu yeni, Guid? hesapId,
        Guid? cariId, decimal tutar, string? aciklama) => new()
    {
        EvrakId = evrak.Id,
        BordroId = bordroId,
        IslemId = islemId,
        Sira = evrak.Hareketler.Count + 1,
        Islem = islem,
        Tarih = tarih,
        OncekiDurum = onceki,
        YeniDurum = yeni,
        HesapId = hesapId,
        CariHesapId = cariId,
        Tutar = tutar,
        Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
    };

    public void OncekileriSakla(Guid? konum, Guid? ciroCari, decimal? ciroTutar, decimal? ciroKur, DateOnly? karsiliksiz)
    {
        OncekiKonumHesapId = konum;
        OncekiCiroCariId = ciroCari;
        OncekiCiroCariTutar = ciroTutar;
        OncekiCiroCariKur = ciroKur;
        OncekiKarsiliksizTarihi = karsiliksiz;
    }

    public void GeriAl(DateTime zaman)
    {
        GeriAlindi = true;
        GeriAlmaTarihi = zaman;
    }
}

// Aynı anda aynı işleme giren evrakların belgesi (giriş/çıkış/ciro/tahsile verme bordrosu).
internal sealed class EvrakBordrosu : FirmaEntity, IDonemeTabi
{
    private EvrakBordrosu() { }

    public string Kod { get; private set; } = null!;
    public EvrakIslemi Islem { get; private set; }
    public DateOnly Tarih { get; private set; }
    public Guid? CariHesapId { get; private set; }
    public Guid? HesapId { get; private set; }
    public string? Aciklama { get; private set; }

    public const string KodOneki = "BR";

    public static EvrakBordrosu Olustur(string kod, EvrakIslemi islem, DateOnly tarih, Guid? cariId, Guid? hesapId, string? aciklama) => new()
    {
        Kod = kod,
        Islem = islem,
        Tarih = tarih,
        CariHesapId = cariId,
        HesapId = hesapId,
        Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
    };
}
