using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Doviz.Domain;

// TCMB bülten kuru, 1 birim döviz için TL. Firmadan bağımsızdır.
internal sealed class Kur : BaseEntity
{
    private Kur() { }

    public DateOnly Tarih { get; private set; }
    public string ParaBirimi { get; private set; } = null!;
    public decimal? DovizAlis { get; private set; }
    public decimal? DovizSatis { get; private set; }
    public decimal? EfektifAlis { get; private set; }
    public decimal? EfektifSatis { get; private set; }

    public static Kur Olustur(DateOnly tarih, TcmbKuru kur) => new()
    {
        Tarih = tarih,
        ParaBirimi = kur.ParaBirimi,
        DovizAlis = kur.DovizAlis,
        DovizSatis = kur.DovizSatis,
        EfektifAlis = kur.EfektifAlis,
        EfektifSatis = kur.EfektifSatis
    };
}

// Bir tarih için TCMB'ye bakıldığını kaydeder; tatil günlerinde (bülten yok) tekrar tekrar istek atılmasın.
internal sealed class KurBulteni : BaseEntity
{
    private KurBulteni() { }

    public DateOnly Tarih { get; private set; }
    public bool YayimlandiMi { get; private set; }

    public static KurBulteni Olustur(DateOnly tarih, bool yayimlandiMi) => new() { Tarih = tarih, YayimlandiMi = yayimlandiMi };
}

internal sealed record TcmbKuru(string ParaBirimi, decimal? DovizAlis, decimal? DovizSatis, decimal? EfektifAlis, decimal? EfektifSatis);
