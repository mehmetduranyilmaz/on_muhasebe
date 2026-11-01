using OnMuhasebe.Modules.Fatura.Contracts;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Personel.Contracts;
using OnMuhasebe.Shared.Contracts.Muhasebe;
using OnMuhasebe.Shared.Domain;

namespace OnMuhasebe.Modules.Muhasebe.Domain;

// Kartın hesap kodu: elle verilen kod, yoksa plandan otomatik kod. Kaynak modülün lisansı yoksa kart listesi boştur: ana hesap kullanılır.
internal sealed class KodCozucu(
    HesapPlani plan, IReadOnlyDictionary<(KartTuru, Guid), string> ozelKodlar,
    IEnumerable<MuhasebeKarti> cariler, IEnumerable<MuhasebeKarti> hesaplar, IEnumerable<MuhasebeKarti> kategoriler)
{
    private readonly Dictionary<Guid, MuhasebeKarti> _cariler = cariler.ToDictionary(k => k.Id);
    private readonly Dictionary<Guid, MuhasebeKarti> _hesaplar = hesaplar.ToDictionary(k => k.Id);
    private readonly Dictionary<Guid, MuhasebeKarti> _kategoriler = kategoriler.ToDictionary(k => k.Id);

    public (string Kod, string Ad) Rol(HesapRolu rol) => (plan.Kod(rol), HesapRolleri.Ad(rol));

    // Hem müşteri hem tedarikçi olan carinin hesabı işlemin yönünden seçilir; cari yoksa (nihai tüketici) ana hesap.
    public (string Kod, string Ad) Cari(Guid? id, bool tedarikciYonu)
    {
        if (id is { } cariId && _cariler.TryGetValue(cariId, out var kart))
        {
            var tedarikciMi = kart.Tur switch { "Tedarikci" => true, "Musteri" => false, _ => tedarikciYonu };
            return (ozelKodlar.GetValueOrDefault((KartTuru.Cari, cariId)) ?? plan.CariKodu(kart.Kod, tedarikciMi), kart.Ad);
        }

        return Rol(tedarikciYonu ? HesapRolu.Saticilar : HesapRolu.Alicilar);
    }

    public (string Kod, string Ad)? Hesap(Guid id) => _hesaplar.TryGetValue(id, out var kart)
        ? (ozelKodlar.GetValueOrDefault((KartTuru.KasaHesabi, id)) ?? plan.HesapKoduOtomatik(kart.Kod, kart.Tur), kart.Ad)
        : null;

    public bool NakitMi(Guid hesapId) => _hesaplar.TryGetValue(hesapId, out var kart) && kart.Tur == "Kasa";

    public (string Kod, string Ad) Kategori(Guid? id, HesapRolu varsayilan) => id is { } kategoriId && _kategoriler.TryGetValue(kategoriId, out var kart)
        ? (ozelKodlar.GetValueOrDefault((KartTuru.Kategori, kategoriId)) ?? plan.KategoriKodu(kart.Tur), kart.Ad)
        : Rol(varsayilan);
}

// Kaynak belgeleri yevmiye fişine çevirir. Tutarlar TL'dir; her kalem ayrı yuvarlanır, cari satırı farkı alarak fişi dengeler.
internal static class FisUretici
{
    private sealed record Kalem((string Kod, string Ad) Hesap, decimal Tutar, string? Aciklama);

    // Satış tarafı: Satış (B cari / A 600, A 391, B 193 kesilen stopaj); satıştan iade tersidir ve 610 ile 191 kullanır.
    // Alış tarafı (alış, gider pusulası, SMM): B 153 stoklu, B 770 stoksuz, B 191 KDV / A 360 tevkifat ve stopaj / A cari;
    // alıştan iade tersidir ve KDV'si 391'dir. Tevkifatta alıcı KDV'nin tamamını indirir, tevkif edilen kısmı 360'a borçlanır.
    public static MuhasebeFisi Fatura(MuhasebeFaturasi f, KodCozucu kodlar)
    {
        var tur = Enum.Parse<FaturaTuru>(f.Tur);
        var iadeMi = tur is FaturaTuru.SatistanIade or FaturaTuru.AlistanIade;
        var y = iadeMi ? -1 : 1;
        var stoklu = Tl(f.StokluMatrah);
        var stoksuz = Tl(f.StoksuzMatrah);
        var kdv = Tl(f.Kdv);
        var tevkif = Tl(f.TevkifEdilenKdv);
        var stopaj = Tl(f.Stopaj);
        var kalemler = new List<Kalem>();

        if (FaturaTurleri.SatisTarafiMi(tur))
        {
            kalemler.Add(new(kodlar.Rol(iadeMi ? HesapRolu.SatistanIadeler : HesapRolu.YurticiSatislar), -y * (stoklu + stoksuz), null));
            kalemler.Add(new(kodlar.Rol(iadeMi ? HesapRolu.IndirilecekKdv : HesapRolu.HesaplananKdv), -y * (kdv - tevkif), "KDV"));
            kalemler.Add(new(kodlar.Rol(HesapRolu.PesinOdenenVergiler), y * stopaj, "Kesilen gelir vergisi stopajı"));
        }
        else
        {
            kalemler.Add(new(kodlar.Rol(HesapRolu.TicariMallar), y * stoklu, null));
            kalemler.Add(new(kodlar.Rol(HesapRolu.HizmetAlimGiderleri), y * stoksuz, null));
            kalemler.Add(new(kodlar.Rol(iadeMi ? HesapRolu.HesaplananKdv : HesapRolu.IndirilecekKdv), y * kdv, "KDV"));
            kalemler.Add(new(kodlar.Rol(HesapRolu.OdenecekVergiler), -y * tevkif, "KDV tevkifatı"));
            kalemler.Add(new(kodlar.Rol(HesapRolu.OdenecekVergiler), -y * stopaj, "Gelir vergisi stopajı"));
        }

        var tedarikciYonu = !FaturaTurleri.SatisTarafiMi(tur);
        kalemler.Insert(0, new(kodlar.Cari(f.CariHesapId, tedarikciYonu), -kalemler.Sum(k => k.Tutar), f.CariUnvan));
        var aciklama = $"{f.CariUnvan} - {BelgeAdi(tur)} {f.Numara}".TrimEnd();
        return new MuhasebeFisi(f.Id, FisKaynaklari.Fatura, f.Tur, FisTuru.Mahsup, f.Tarih, f.Numara, aciklama, f.IsyeriId, Satirlar(kalemler, aciklama));

        decimal Tl(decimal tutar) => Para.TlKarsiligi(tutar, f.Kur);
    }

    // Aynı işlemin hareketleri (virmanın iki bacağı, kredi kullanımı ve masrafı) tek fiştir. Karşı hesap sırası:
    // kaynak modülün bildirdiği rol (çek/senet, kredi) > gelir-gider kategorisi > cari > (carisiz tahsilat/ödeme) alıcı/satıcı ana hesabı.
    public static IReadOnlyList<MuhasebeFisi> KasaBanka(IEnumerable<MuhasebeKasaHareketi> hareketler, KodCozucu kodlar, ICollection<string> uyarilar)
    {
        var fisler = new List<MuhasebeFisi>();
        foreach (var islem in hareketler.GroupBy(h => h.IslemId))
        {
            var ilk = islem.First();
            var kalemler = new List<Kalem>();
            foreach (var h in islem)
            {
                var tutar = h.GirisTl - h.CikisTl;
                if (kodlar.Hesap(h.HesapId) is not { } hesap)
                {
                    uyarilar.Add($"{h.Tarih:dd.MM.yyyy} {h.BelgeNo} {h.Tur}: kasa/banka hesabı bulunamadı.");
                    hesap = ("", "Bilinmeyen hesap");
                }

                kalemler.Add(new(hesap, tutar, h.Aciklama));
                if (h.Tur is "VirmanGiris" or "VirmanCikis")
                {
                    continue;
                }

                var gider = Math.Sign(-tutar) * h.GiderTl;
                if (gider != 0)
                {
                    kalemler.Add(new(kodlar.Kategori(h.GiderKategoriId, HesapRolu.FinansmanGiderleri), gider, "Faiz ve vergiler"));
                }

                kalemler.Add(new(Karsi(h, tutar), -tutar - gider, h.Aciklama));
            }

            var fark = kalemler.Sum(k => k.Tutar);
            if (fark != 0)
            {
                if (islem.Any(h => h.Tur == "VirmanGiris") && islem.Any(h => h.Tur == "VirmanCikis"))
                {
                    kalemler.Add(new(kodlar.Rol(fark > 0 ? HesapRolu.KambiyoKarlari : HesapRolu.KambiyoZararlari), -fark, "Virman kur farkı"));
                }
                else
                {
                    uyarilar.Add($"{ilk.Tarih:dd.MM.yyyy} {ilk.BelgeNo} virman: karşı hesap bu aktarımda yok (başka işyerinde olabilir).");
                }
            }

            var aciklama = ilk.Aciklama ?? ilk.Tur;
            fisler.Add(new MuhasebeFisi(islem.Key, FisKaynaklari.KasaBanka, ilk.Tur, Tur(islem), ilk.Tarih, ilk.BelgeNo, aciklama, ilk.IsyeriId,
                Satirlar(kalemler, aciklama)));
        }

        return fisler;

        (string Kod, string Ad) Karsi(MuhasebeKasaHareketi h, decimal tutar)
        {
            if (h.KarsiRol is { } rolAdi && Enum.TryParse<HesapRolu>(rolAdi, out var rol))
            {
                return kodlar.Rol(rol);
            }

            if (h.KategoriId is not null || h.Tur is "Gelir" or "Gider")
            {
                return kodlar.Kategori(h.KategoriId, h.Tur == "Gelir" ? HesapRolu.DigerGelirler : HesapRolu.GenelYonetimGiderleri);
            }

            if (h.CariHesapId is not null || h.Tur is "Tahsilat" or "Odeme")
            {
                return kodlar.Cari(h.CariHesapId, tedarikciYonu: tutar < 0);
            }

            uyarilar.Add($"{h.Tarih:dd.MM.yyyy} {h.BelgeNo} {h.Tur}: karşı hesap belirlenemedi.");
            return ("", "Karşı hesap belirlenemedi");
        }

        // Yalnız nakit kasanın tek yönlü hareketi tahsil/tediye fişidir.
        FisTuru Tur(IEnumerable<MuhasebeKasaHareketi> islem)
        {
            var liste = islem.ToList();
            if (liste.Any(h => !kodlar.NakitMi(h.HesapId) || h.Tur is "VirmanGiris" or "VirmanCikis"))
            {
                return FisTuru.Mahsup;
            }

            return liste.All(h => h.GirisTl > 0) ? FisTuru.Tahsil : liste.All(h => h.CikisTl > 0) ? FisTuru.Tediye : FisTuru.Mahsup;
        }
    }

    // Kasayı etkilemeyen çek/senet kayıtları; bordro başına tek fiş. Alınan evrak 101/121'de, verilen 103/321'de izlenir.
    // Karşılıksız evrakın tutarı müşteriye geri yazılır (programdaki cari bakiyesiyle aynı); sonraki tahsilat cariyi kapatır.
    public static IReadOnlyList<MuhasebeFisi> CekSenet(IEnumerable<MuhasebeEvrakHareketi> hareketler, KodCozucu kodlar) =>
        hareketler.GroupBy(h => h.BordroId).Select(bordro =>
        {
            var ilk = bordro.First();
            var kalemler = new List<Kalem>();
            foreach (var h in bordro)
            {
                var senetMi = h.EvrakTuru == "Senet";
                var alinan = kodlar.Rol(senetMi ? HesapRolu.AlacakSenetleri : HesapRolu.AlinanCekler);
                var verilen = kodlar.Rol(senetMi ? HesapRolu.BorcSenetleri : HesapRolu.VerilenCekler);
                var musteri = kodlar.Cari(h.CariHesapId, tedarikciYonu: false);
                var tedarikci = kodlar.Cari(h.CariHesapId, tedarikciYonu: true);
                var ciroCari = kodlar.Cari(h.CiroCariId, tedarikciYonu: true);
                var (borc, alacak) = h.Islem switch
                {
                    "Giris" => (alinan, musteri),
                    "Cikis" => (tedarikci, verilen),
                    "Ciro" => (ciroCari, alinan),
                    "CirodanGeriAlma" => (alinan, ciroCari),
                    "CirodanKarsiliksiz" => (musteri, ciroCari),
                    "Karsiliksiz" or "Iade" => (musteri, alinan),
                    _ => (verilen, tedarikci)
                };
                var aciklama = $"{h.EvrakNo} vade {h.Vade:dd.MM.yyyy} - {EvrakIslemAdi(h.Islem)}";
                kalemler.Add(new(borc, h.TutarTl, aciklama));
                kalemler.Add(new(alacak, -h.TutarTl, aciklama));
            }

            var fisAciklamasi = $"Çek/senet bordrosu {ilk.BordroNo}: {EvrakIslemAdi(ilk.Islem)}";
            return new MuhasebeFisi(bordro.Key, FisKaynaklari.CekSenet, ilk.Islem, FisTuru.Mahsup, ilk.Tarih, ilk.BordroNo, fisAciklamasi, null,
                Satirlar(kalemler, fisAciklamasi));
        }).ToList();

    // Ödenmemiş gider: B gider kategorisi / A cari; tahakkuk eden gelir: B cari / A gelir kategorisi. Ödeme sonra kasa fişiyle kapanır.
    public static IReadOnlyList<MuhasebeFisi> Tahakkuk(IEnumerable<MuhasebeTahakkuk> kayitlar, KodCozucu kodlar) =>
        kayitlar.GroupBy(t => t.IslemId).Select(islem =>
        {
            var ilk = islem.First();
            var kalemler = new List<Kalem>();
            foreach (var t in islem)
            {
                var gelirMi = t.Tur == "Gelir";
                var kategori = kodlar.Kategori(t.KategoriId, gelirMi ? HesapRolu.DigerGelirler : HesapRolu.GenelYonetimGiderleri);
                var cari = kodlar.Cari(t.CariHesapId, tedarikciYonu: !gelirMi);
                var tutar = gelirMi ? -t.TutarTl : t.TutarTl;
                kalemler.Add(new(kategori, tutar, t.Aciklama));
                kalemler.Add(new(cari, -tutar, t.Aciklama));
            }

            var aciklama = ilk.Aciklama ?? (ilk.Tur == "Gelir" ? "Gelir tahakkuku" : "Gider tahakkuku");
            return new MuhasebeFisi(islem.Key, FisKaynaklari.Tahakkuk, ilk.Tur, FisTuru.Mahsup, ilk.Tarih, ilk.BelgeNo, aciklama, null, Satirlar(kalemler, aciklama));
        }).ToList();

    // Ödemeler kasa hareketinden gelir. Burada kasadan geçmeyenler: onaylı masraf (gider / 335) ve maaştan düşülen avans (335 / 196).
    public static IEnumerable<MuhasebeFisi> Personel(IEnumerable<MuhasebePersonelKaydi> kayitlar, KodCozucu kodlar) => kayitlar.Select(k =>
    {
        var borc = kodlar.Rol(k.Tur == MuhasebePersonelTurleri.Masraf ? HesapRolu.GenelYonetimGiderleri : HesapRolu.PersonelBorclari);
        var alacak = kodlar.Rol(k.Tur == MuhasebePersonelTurleri.Masraf ? HesapRolu.PersonelBorclari : HesapRolu.PersonelAvanslari);
        return new MuhasebeFisi(k.Id, FisKaynaklari.Personel, k.Tur, FisTuru.Mahsup, k.Tarih, k.BelgeNo, k.Aciklama, k.IsyeriId,
            Satirlar([new(borc, k.Tutar, k.Aciklama), new(alacak, -k.Tutar, k.Aciklama)], k.Aciklama));
    }).ToList();

    private static string EvrakIslemAdi(string islem) => islem switch
    {
        "Giris" => "portföye giriş",
        "Cikis" => "tedarikçiye verildi",
        "Ciro" => "ciro edildi",
        "CirodanGeriAlma" => "cirodan geri alındı",
        "CirodanKarsiliksiz" => "ciro edilen evrak karşılıksız",
        "Karsiliksiz" => "karşılıksız",
        "Iade" => "müşteriye iade",
        "VerilenIade" => "verilen evrak iade alındı",
        _ => "verilen evrak karşılıksız"
    };

    private static IReadOnlyList<FisSatiri> Satirlar(IEnumerable<Kalem> kalemler, string aciklama) => kalemler
        .Where(k => k.Tutar != 0)
        .Select(k => new FisSatiri(k.Hesap.Kod, k.Hesap.Ad, k.Tutar > 0 ? k.Tutar : 0, k.Tutar < 0 ? -k.Tutar : 0, k.Aciklama ?? aciklama))
        .ToList();

    private static string BelgeAdi(FaturaTuru tur) => tur switch
    {
        FaturaTuru.Satis => "Satış faturası",
        FaturaTuru.Alis => "Alış faturası",
        FaturaTuru.SatistanIade => "Satıştan iade faturası",
        FaturaTuru.AlistanIade => "Alıştan iade faturası",
        FaturaTuru.GiderPusulasi => "Gider pusulası",
        _ => "Serbest meslek makbuzu"
    };
}
