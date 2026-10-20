using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Web.KasaBanka.Bilesenler;

// Ekstre satırı ya da eşleştirme kuralı için işlem türü ve hedefi; HedefSecici bunu düzenler.
public sealed class EkstreSecimi
{
    public EkstreIslemTuru Tur { get; set; }
    public CariSecim? Cari { get; set; }
    public Guid? KategoriId { get; set; }
    public Guid? KarsiHesapId { get; set; }

    // Türün gerektirmediği hedefler gönderilmez.
    public Guid? GonderilecekCari => EkstreMetinleri.CariGerekliMi(Tur) ? Cari?.Id : null;
    public Guid? GonderilecekKategori => EkstreMetinleri.KategoriGerekliMi(Tur) ? KategoriId : null;
    public Guid? GonderilecekKarsiHesap => Tur == EkstreIslemTuru.Virman ? KarsiHesapId : null;

    public string? Eksik => EkstreMetinleri.Eksik(Tur, GonderilecekCari, GonderilecekKategori, GonderilecekKarsiHesap);
}

public sealed record CariSecim(Guid Id, string Unvan, string? Kod = null);
