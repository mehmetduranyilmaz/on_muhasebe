using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Contracts.Moduller;

namespace OnMuhasebe.Modules.KasaBanka.Contracts;

public static class KasaBankaYetkileri
{
    private const string Baslik = "Kasa / Banka";

    public static readonly YetkiKaynagi Hesap = new("kasabanka.hesap", ModulKodlari.KasaBanka, Baslik, "Kasa, banka, kart ve POS hesapları",
        YetkiIslemleri.Temel, ["kasabanka.tanim.goruntule"]);

    public static readonly YetkiKaynagi Islem = new("kasabanka.islem", ModulKodlari.KasaBanka, Baslik, "Tahsilat, ödeme, virman, gelir-gider",
        YetkiIslemleri.Hareket, ["kasabanka.hesap.goruntule", "kasabanka.tanim.goruntule", "cari.hesap.goruntule"]);

    public static readonly YetkiKaynagi Tanim = new("kasabanka.tanim", ModulKodlari.KasaBanka, Baslik, "Kategoriler, masraf merkezleri, banka şubeleri",
        YetkiIslemleri.Temel, []);

    public static readonly YetkiKaynagi Ekstre = new("kasabanka.ekstre", ModulKodlari.KasaBanka, Baslik, "Banka ekstresi aktarımı ve kuralları",
        YetkiIslemleri.Temel, ["kasabanka.hesap.goruntule", "kasabanka.tanim.goruntule", "cari.hesap.goruntule"]);

    public static readonly YetkiKaynagi Cek = new("kasabanka.cek", ModulKodlari.KasaBanka, Baslik, "Çek ve senet (bordro, karne, borçlular)",
        YetkiIslemleri.Belge, ["kasabanka.hesap.goruntule", "cari.hesap.goruntule"]);

    public static readonly YetkiKaynagi Teminat = new("kasabanka.teminat", ModulKodlari.KasaBanka, Baslik, "Teminat ve ipotekler",
        YetkiIslemleri.Temel, ["cari.hesap.goruntule"]);

    public static readonly YetkiKaynagi Kredi = new("kasabanka.kredi", ModulKodlari.KasaBanka, Baslik, "Krediler ve taksit ödemeleri",
        YetkiIslemleri.Hareket, ["kasabanka.hesap.goruntule"]);

    public static readonly YetkiKaynagi Demirbas = new("kasabanka.demirbas", ModulKodlari.KasaBanka, Baslik, "Demirbaşlar ve amortisman",
        YetkiIslemleri.Temel, ["kasabanka.tanim.goruntule"]);

    public static readonly YetkiKaynagi Rapor = new("kasabanka.rapor", ModulKodlari.KasaBanka, Baslik, "Kasa/banka raporları ve nakit akışı",
        YetkiIslemleri.SadeceGoruntule, ["kasabanka.hesap.goruntule"]);

    public static IReadOnlyList<YetkiKaynagi> Tumu { get; } = [Hesap, Islem, Tanim, Ekstre, Cek, Teminat, Kredi, Demirbas, Rapor];
}
