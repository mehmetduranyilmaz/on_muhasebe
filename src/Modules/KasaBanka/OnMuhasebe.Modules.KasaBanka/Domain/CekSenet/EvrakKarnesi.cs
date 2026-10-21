using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

// Firmanın kendi çek karnesi (banka hesabına bağlı) ya da senet defteri: önek + başlangıç numarasından ardışık yapraklar.
// Kullanılan yaprak, karneye bağlı verilen evraklardan bulunur; yırtılan/kaybolan yaprak iptal edilir ve bir daha önerilmez.
internal sealed class EvrakKarnesi : FirmaEntity
{
    public const int AzamiYaprak = 500;
    public const int NedenUzunlugu = 200;

    private readonly List<KarneYaprakIptali> _iptaller = [];

    private EvrakKarnesi() { }

    public EvrakTuru Tur { get; private set; }
    public Guid? KendiHesapId { get; private set; }
    public string Onek { get; private set; } = null!;
    public long BaslangicNo { get; private set; }
    public int Hane { get; private set; }
    public int YaprakSayisi { get; private set; }
    public DateOnly Tarih { get; private set; }
    public string? Aciklama { get; private set; }
    public bool AktifMi { get; private set; } = true;

    public IReadOnlyList<KarneYaprakIptali> Iptaller => _iptaller;

    public long BitisNo => BaslangicNo + YaprakSayisi - 1;

    public static Sonuc<EvrakKarnesi> Olustur(EvrakTuru tur, Guid? kendiHesapId, string baslangicSeri, int yaprakSayisi, DateOnly tarih, string? aciklama)
    {
        if (EvrakSeriNo.Ayir(baslangicSeri) is not { } seri)
        {
            return KasaBankaHatalari.KarneSeriGecersiz;
        }

        return new EvrakKarnesi
        {
            Tur = tur,
            KendiHesapId = tur == EvrakTuru.Cek ? kendiHesapId : null,
            Onek = seri.Onek,
            BaslangicNo = seri.Sayi,
            Hane = seri.Hane,
            YaprakSayisi = yaprakSayisi,
            Tarih = tarih,
            Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim()
        };
    }

    public string Seri(long no) => EvrakSeriNo.Olustur(Onek, no, Hane);

    public IEnumerable<string> Seriler()
    {
        for (var no = BaslangicNo; no <= BitisNo; no++)
        {
            yield return Seri(no);
        }
    }

    public bool Icerir(string seri) =>
        EvrakSeriNo.Ayir(seri) is { } s && s.Onek == Onek && s.Sayi >= BaslangicNo && s.Sayi <= BitisNo;

    // Aynı öneki taşıyan iki karnenin numara aralığı kesişirse aynı yaprak iki kez tanımlanmış olur.
    public bool Cakisir(EvrakKarnesi diger) =>
        diger.Tur == Tur && diger.KendiHesapId == KendiHesapId && diger.Onek == Onek && diger.BaslangicNo <= BitisNo && BaslangicNo <= diger.BitisNo;

    public bool IptalMi(string seri) => _iptaller.Any(i => i.SeriNo == EvrakSeriNo.Normalize(seri));

    // Kullanılmamış ve iptal edilmemiş ilk yaprak.
    public string? Siradaki(IReadOnlySet<string> kullanilanlar) =>
        Seriler().FirstOrDefault(s => !kullanilanlar.Contains(s) && !IptalMi(s));

    public int Kalan(IReadOnlySet<string> kullanilanlar) => Seriler().Count(s => !kullanilanlar.Contains(s) && !IptalMi(s));

    public Sonuc YapragiIptalEt(string seri, string neden, DateOnly tarih, IReadOnlySet<string> kullanilanlar)
    {
        var normal = EvrakSeriNo.Normalize(seri);
        if (!Icerir(normal))
        {
            return KasaBankaHatalari.KarneSeriDisinda;
        }

        if (kullanilanlar.Contains(normal))
        {
            return KasaBankaHatalari.KarneYapragiKullanildi;
        }

        if (IptalMi(normal))
        {
            return KasaBankaHatalari.KarneYapragiIptal;
        }

        _iptaller.Add(KarneYaprakIptali.Olustur(Id, normal, neden.Trim(), tarih));
        return Sonuc.Basarili();
    }

    public Sonuc IptaliGeriAl(string seri)
    {
        var iptal = _iptaller.FirstOrDefault(i => i.SeriNo == EvrakSeriNo.Normalize(seri));
        if (iptal is null)
        {
            return KasaBankaHatalari.KarneYapragiIptalDegil;
        }

        _iptaller.Remove(iptal);
        return Sonuc.Basarili();
    }

    public void Kapat() => AktifMi = false;

    public void Ac() => AktifMi = true;
}

internal sealed class KarneYaprakIptali : BaseEntity
{
    private KarneYaprakIptali() { }

    public Guid KarneId { get; private set; }
    public string SeriNo { get; private set; } = null!;
    public string Neden { get; private set; } = null!;
    public DateOnly Tarih { get; private set; }

    public static KarneYaprakIptali Olustur(Guid karneId, string seriNo, string neden, DateOnly tarih) =>
        new() { KarneId = karneId, SeriNo = seriNo, Neden = neden, Tarih = tarih };
}
