using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Fatura.Domain;

// Firma başına tek satır; kayıt yoksa varsayılanlar geçerlidir.
internal sealed class FaturaAyari : FirmaEntity
{
    private FaturaAyari() { }

    public KayitAkisi KayitAkisi { get; private set; } = KayitAkisi.DogrudanKayit;
    public string ToptanSeri { get; private set; } = "TPT";
    public string PerakendeSeri { get; private set; } = "PRK";
    public string IadeSeri { get; private set; } = "IAD";
    public string GiderPusulasiSeri { get; private set; } = "GPS";
    public string IrsaliyeSeri { get; private set; } = "IRS";
    public bool ToptanKdvDahil { get; private set; }
    public bool PerakendeKdvDahil { get; private set; } = true;
    public int VarsayilanVadeGunu { get; private set; }

    public static FaturaAyari Varsayilan() => new();

    public void Guncelle(FaturaAyarlari a)
    {
        KayitAkisi = a.KayitAkisi;
        ToptanSeri = a.ToptanSeri.ToUpperInvariant();
        PerakendeSeri = a.PerakendeSeri.ToUpperInvariant();
        IadeSeri = a.IadeSeri.ToUpperInvariant();
        GiderPusulasiSeri = a.GiderPusulasiSeri.ToUpperInvariant();
        IrsaliyeSeri = a.IrsaliyeSeri.ToUpperInvariant();
        ToptanKdvDahil = a.ToptanKdvDahil;
        PerakendeKdvDahil = a.PerakendeKdvDahil;
        VarsayilanVadeGunu = a.VarsayilanVadeGunu;
    }

    // Satış (toptan/perakende), alıştan iade ve gider pusulası numarayı biz veririz; diğer türlerin serisi yoktur.
    public string Seri(FaturaTuru tur, SatisSekli? satisSekli) => tur switch
    {
        FaturaTuru.Satis => satisSekli == SatisSekli.Perakende ? PerakendeSeri : ToptanSeri,
        FaturaTuru.AlistanIade => IadeSeri,
        FaturaTuru.GiderPusulasi => GiderPusulasiSeri,
        _ => throw new InvalidOperationException($"{tur} belgesinin numarasını karşı taraf verir.")
    };

    public FaturaAyarlari Sozlesme() =>
        new(KayitAkisi, ToptanSeri, PerakendeSeri, IadeSeri, ToptanKdvDahil, PerakendeKdvDahil, VarsayilanVadeGunu, GiderPusulasiSeri, IrsaliyeSeri);
}

// Seri + yıl başına son verilen sıra. Satır, numara alan transaction bitene kadar kilitli kalır; böylece
// eşzamanlı faturalar sırayla numara alır ve geri alınan işlem numara atlatmaz (GİB sıra kuralı).
internal sealed class FaturaSeriSayaci
{
    private FaturaSeriSayaci() { }

    public Guid FirmaId { get; private set; }
    public string Seri { get; private set; } = null!;
    public int Yil { get; private set; }
    public long SonNumara { get; private set; }
}
