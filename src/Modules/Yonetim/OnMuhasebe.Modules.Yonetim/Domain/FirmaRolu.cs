using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Entities;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Yonetim.Domain;

// Firmanın tanımladığı rol. Tam yetkili rol (Yönetici) her firmada tektir; değiştirilemez, silinemez ve her yetkiyi
// (sonradan kiralanan modüllerinkiler dahil) kendiliğinden kapsar.
internal sealed class FirmaRolu : FirmaEntity
{
    public const string YoneticiAdi = "Yönetici";

    private FirmaRolu() { }

    private FirmaRolu(Guid firmaId) : base(firmaId) { }

    public string Ad { get; private set; } = null!;
    public string? Aciklama { get; private set; }
    public bool TamYetkiliMi { get; private set; }

    // Mali müşavir rolü: yalnız görüntüleme ve muhasebe aktarımı yetkisi taşır; bu roldeki kullanıcı paketin kullanıcı limitine sayılmaz.
    public bool MaliMusavirMi { get; private set; }
    public List<string> Yetkiler { get; private set; } = [];

    // firmaId yalnızca oturum dışı işlemlerde (kayıt, sistem yöneticisinin firma açması) verilir.
    public static FirmaRolu Olustur(string ad, string? aciklama, IEnumerable<string> yetkiler, Guid? firmaId = null, bool maliMusavirMi = false)
    {
        var rol = firmaId is null ? new FirmaRolu() : new FirmaRolu(firmaId.Value);
        rol.MaliMusavirMi = maliMusavirMi;
        rol.Ad = ad.Trim();
        rol.Aciklama = Temiz(aciklama);
        rol.Yetkiler = yetkiler.Distinct().Order().ToList();
        return rol;
    }

    public static FirmaRolu YoneticiOlustur(Guid? firmaId = null)
    {
        var rol = Olustur(YoneticiAdi, "Tüm yetkiler; kiralanan yeni modüller de kendiliğinden dahil olur.", [], firmaId);
        rol.TamYetkiliMi = true;
        return rol;
    }

    public Sonuc Guncelle(string ad, string? aciklama, IEnumerable<string> yetkiler, bool maliMusavirMi)
    {
        if (TamYetkiliMi)
        {
            return YonetimHatalari.YoneticiRoluDegistirilemez;
        }

        MaliMusavirMi = maliMusavirMi;
        Ad = ad.Trim();
        Aciklama = Temiz(aciklama);
        Yetkiler = yetkiler.Distinct().Order().ToList();
        return Sonuc.Basarili();
    }

    // Mali müşavir rolünde verilebilecek yetki: her türlü görüntüleme ve muhasebe aktarımı.
    public static bool MaliMusavirYetkisiMi(string yetki) =>
        yetki.EndsWith("." + YetkiIslemleri.Goruntule, StringComparison.Ordinal) || yetki.StartsWith("muhasebe.aktarim.", StringComparison.Ordinal);

    private static string? Temiz(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}

// Yeni firmaya açılan hazır roller. Firma bunları değiştirebilir ya da silebilir; yalnızca Yönetici sabittir.
internal static class RolSablonlari
{
    public static IReadOnlyList<FirmaRolu> Olustur(IReadOnlyList<YetkiKaynagi> katalog, Guid firmaId)
    {
        var modul = katalog.Where(k => k.Modul.Length > 0).ToList();
        IEnumerable<string> Hepsi(IEnumerable<YetkiKaynagi> kaynaklar) => kaynaklar.SelectMany(k => k.Yetkiler());
        IEnumerable<string> Sec(params string[] yetkiler) => yetkiler;
        IEnumerable<string> Islemler(string kaynak, params string[] islemler) => islemler.Select(i => $"{kaynak}.{i}");

        const string G = YetkiIslemleri.Goruntule, E = YetkiIslemleri.Ekle, D = YetkiIslemleri.Duzenle,
            S = YetkiIslemleri.Sil, O = YetkiIslemleri.Onayla, I = YetkiIslemleri.Iptal;

        List<(string Ad, string Aciklama, IEnumerable<string> Yetkiler)> sablonlar =
        [
            ("Kullanıcı", "Yönetim ekranları dışında tüm işlemler.", Hepsi(modul)),
            ("Muhasebe", "Tüm işlemler, maliyet ve değişiklik geçmişi; kullanıcı ve firma ayarları hariç.",
                Hepsi(modul).Append(YonetimYetkileri.Degisiklik.Yetki(G))),
            ("Satış", "Müşteri, teklif/sipariş, satış belgeleri ve tahsilat; maliyet görmez.",
                [.. Islemler("cari.hesap", G, E, D), .. Islemler("cari.hareket", G), .. Islemler("cari.rapor", G),
                 .. Islemler("stok.kart", G), .. Islemler("stok.fiyat", G), .. Islemler("stok.rapor", G),
                 .. Islemler("fatura.teklif", G, E, D, S, O, I), .. Islemler("fatura.belge", G, E, D),
                 .. Islemler("fatura.irsaliye", G, E, D), .. Islemler("kasabanka.islem", G, E)]),
            ("Satın Alma", "Tedarikçi, stok kartı, sipariş ve alış belgeleri; maliyet görür.",
                [.. Islemler("cari.hesap", G, E, D), .. Islemler("cari.hareket", G),
                 .. Islemler("stok.kart", G, E, D), .. Islemler("stok.fiyat", G, E, D), .. Islemler("stok.rapor", G), .. Sec("stok.maliyet.goruntule"),
                 .. Islemler("fatura.teklif", G, E, D, S, O, I), .. Islemler("fatura.belge", G, E, D), .. Islemler("fatura.irsaliye", G, E, D)]),
            ("Depo", "Stok hareketleri, transfer ve irsaliye.",
                [.. Islemler("stok.kart", G), .. Islemler("stok.tanim", G), .. Islemler("stok.hareket", G, E, I), .. Islemler("stok.rapor", G),
                 .. Islemler("fatura.irsaliye", G, E, D, O), .. Islemler("fatura.teklif", G)]),
            ("Kasa", "Tahsilat, ödeme, virman ve çek/senet girişi.",
                [.. Islemler("cari.hesap", G), .. Islemler("cari.hareket", G), .. Islemler("cari.rapor", G),
                 .. Islemler("kasabanka.hesap", G), .. Islemler("kasabanka.islem", G, E, I), .. Islemler("kasabanka.cek", G, E, D),
                 .. Islemler("kasabanka.rapor", G)]),
        ];

        // Mali müşavir: tüm kayıt ve raporları görür, muhasebe fişlerini indirip dönemi "aktarıldı" işaretler; kullanıcı limitine sayılmaz.
        var maliMusavir = katalog.Where(k => k.Islemler.Contains(G) && k != YonetimYetkileri.Kullanici && k != YonetimYetkileri.Abonelik)
            .Select(k => k.Yetki(G)).Append("muhasebe.aktarim.ekle");

        // Şablondaki yetki katalogda yoksa (modül kaldırılmış vb.) atlanır.
        var gecerli = katalog.SelectMany(k => k.Yetkiler()).ToHashSet();
        return
        [
            FirmaRolu.YoneticiOlustur(firmaId),
            .. sablonlar.Select(s => FirmaRolu.Olustur(s.Ad, s.Aciklama, s.Yetkiler.Where(gecerli.Contains), firmaId)),
            FirmaRolu.Olustur("Salt Okur (Mali Müşavir)", "Tüm kayıt ve raporları görür, maliyet dahil; muhasebe fişlerini indirir. Kullanıcı limitine sayılmaz.",
                maliMusavir.Where(gecerli.Contains), firmaId, maliMusavirMi: true)
        ];
    }
}

// Etkin yetki: rol ∪ ek − yasak; ardından bağımlı yetkiler eklenir. Bir kaynakta yetkisi olan o kaynağı görür ve
// kaynağın Gerekenler'ini (ör. fatura kesen cari listesini) alır; bunlar yasakla kaldırılamaz, yoksa ekran çalışmaz.
internal static class YetkiHesaplayici
{
    public static IReadOnlyList<string> Hesapla(
        IEnumerable<string> rolYetkileri, IEnumerable<string> ek, IEnumerable<string> yasak, IReadOnlyList<YetkiKaynagi> katalog)
    {
        var gecerli = katalog.SelectMany(k => k.Yetkiler()).ToHashSet();
        var sonuc = rolYetkileri.Union(ek).Except(yasak).Where(gecerli.Contains).ToHashSet();

        bool degisti;
        do
        {
            degisti = false;
            foreach (var kaynak in katalog.Where(k => k.Yetkiler().Any(sonuc.Contains)))
            {
                string[] gorme = kaynak.Islemler.Contains(YetkiIslemleri.Goruntule) ? [kaynak.Yetki(YetkiIslemleri.Goruntule)] : [];
                foreach (var y in gorme.Concat(kaynak.Gerekenler))
                {
                    degisti |= sonuc.Add(y);
                }
            }
        }
        while (degisti);

        return sonuc.Order().ToList();
    }
}
