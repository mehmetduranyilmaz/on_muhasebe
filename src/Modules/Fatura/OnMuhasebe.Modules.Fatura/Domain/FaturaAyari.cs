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
    // İşyerinin kendi serisi varsa o, yoksa firma serisi kullanılır.
    public string Seri(FaturaTuru tur, SatisSekli? satisSekli, IsyeriSerisi? ozel = null) => tur switch
    {
        FaturaTuru.Satis => satisSekli == SatisSekli.Perakende ? ozel?.PerakendeSeri ?? PerakendeSeri : ozel?.ToptanSeri ?? ToptanSeri,
        FaturaTuru.AlistanIade => ozel?.IadeSeri ?? IadeSeri,
        FaturaTuru.GiderPusulasi => ozel?.GiderPusulasiSeri ?? GiderPusulasiSeri,
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

// İşyerine özel belge serisi (ör. Ankara şubesi satışları ANK). Boş bırakılan seri firma ayarındakini kullanır.
// Sayaç seri + yıl başınadır: iki işyeri aynı seriyi kullanırsa numaralar ortak sıradan verilir.
internal sealed class IsyeriSerisi : FirmaEntity
{
    private IsyeriSerisi() { }

    public Guid IsyeriId { get; private set; }
    public string? ToptanSeri { get; private set; }
    public string? PerakendeSeri { get; private set; }
    public string? IadeSeri { get; private set; }
    public string? GiderPusulasiSeri { get; private set; }
    public string? IrsaliyeSeri { get; private set; }

    public static IsyeriSerisi Olustur(Guid isyeriId) => new() { IsyeriId = isyeriId };

    public void Guncelle(IsyeriSerileri s)
    {
        ToptanSeri = Seri(s.ToptanSeri);
        PerakendeSeri = Seri(s.PerakendeSeri);
        IadeSeri = Seri(s.IadeSeri);
        GiderPusulasiSeri = Seri(s.GiderPusulasiSeri);
        IrsaliyeSeri = Seri(s.IrsaliyeSeri);
    }

    public bool BosMu => ToptanSeri is null && PerakendeSeri is null && IadeSeri is null && GiderPusulasiSeri is null && IrsaliyeSeri is null;

    public IsyeriSerileri Sozlesme() => new(IsyeriId, ToptanSeri, PerakendeSeri, IadeSeri, GiderPusulasiSeri, IrsaliyeSeri);

    private static string? Seri(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim().ToUpperInvariant();
}
