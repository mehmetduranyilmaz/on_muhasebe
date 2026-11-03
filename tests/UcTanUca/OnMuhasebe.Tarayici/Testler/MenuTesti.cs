using System.Net.Http.Headers;

namespace OnMuhasebe.Tarayici.Testler;

// Sol menü grupları: sıra, açılıp kapanma, hatırlama, e-Belge yeri, yetkiye göre gizlenen grup ve başlıksız tek madde.
internal sealed class MenuTesti : TarayiciTesti
{
    private static readonly string[] CalisanYetkileri = ["personel.kendi.goruntule", "personel.kendi.ekle"];

    public override string Ad => "menu";

    private const string Basliklar = "[...document.querySelectorAll('.mud-navmenu .menu-grubu > .mud-nav-link .mud-nav-link-text')].map(e => e.textContent.trim()).join('|')";

    private static string GrupAcikMi(string ad) => $$"""
        (() => { const g = [...document.querySelectorAll('.mud-navmenu .menu-grubu')].find(x => x.querySelector('.mud-nav-link-text')?.textContent.trim() === {{S(ad)}});
          return !!g?.querySelector('.mud-collapse-entered'); })()
        """;

    private static string GrupTikla(string ad) => $$"""
        [...document.querySelectorAll('.mud-navmenu .menu-grubu > .mud-nav-link')].find(x => x.textContent.trim().startsWith({{S(ad)}}))?.click()
        """;

    protected override async Task CalistirAsync()
    {
        await Komut("Page.enable");
        var damga = DateTime.UtcNow.Ticks;
        var eposta = $"menuarayuz{damga}@ornek.com";
        var calisan = $"menucalisan{damga}@ornek.com";
        var kayit = await KayitOlAsync(new { firmaUnvani = "Menu Arayuz Ltd", ad = "Mine", soyad = "Yon", eposta, sifre = "Sifre1234" });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kayit["accessToken"]!.GetValue<string>());
        var rol = await Post("api/yonetim/roller", new { ad = "Calisan", yetkiler = CalisanYetkileri });
        await Post("api/yonetim/kullanicilar", new { eposta = calisan, rolId = rol["id"]!.GetValue<string>() });
        var kod = await MailpitKoduAsync(calisan, "davet");
        await Post("api/yonetim/davet-kabul", new { kod, ad = "Can", soyad = "Calisan", sifre = "Sifre1234" });

        await GirisYap(eposta, "Sifre1234");
        Kontrol(await Bekle($"{Basliklar} === 'Stok ve Fiyat|Satış ve Satın Alma|Finans|Muhasebe|Personel|Takip|Yönetim'", 15), "Gruplar dogru sirada");
        Kontrol(await Js("[...document.querySelectorAll('.mud-navmenu > a, .mud-navmenu > .mud-nav-item a')][0]?.getAttribute('href')") == "cari", "Cari Hesaplar basliksiz en ustte");
        Kontrol(await Js("(() => { const d = document.querySelector('.mud-drawer .mud-drawer-content'); return d.scrollWidth <= d.clientWidth; })()") == "true", "Menude yatay tasma yok");
        await Ekran("1-menu");

        Kontrol(await Bekle($"!{GrupAcikMi("Finans")}", 5), "Ana sayfada gruplar kapali");
        await Js(GrupTikla("Satış ve Satın Alma"));
        Kontrol(await Bekle(GrupAcikMi("Satış ve Satın Alma"), 5), "Grup tiklaninca acildi");
        await Task.Delay(400);
        Kontrol(await Js("(() => { const y = document.querySelector('.menu-yakinda'); return !!y && y.textContent.includes('e-Belge') && y.textContent.includes('Yakında') && !y.closest('a'); })()") == "true", "e-Belge yakinda, tiklanamaz");
        Kontrol(await Js("(() => { const f = document.querySelector('.mud-navmenu a[href=\"fatura\"]').closest('.mud-nav-item') ?? document.querySelector('.mud-navmenu a[href=\"fatura\"]'); return f.nextElementSibling?.classList.contains('menu-yakinda'); })()") == "true", "e-Belge Faturanin hemen altinda");
        await Ekran("2-satis-acik");

        await Git("/");
        Kontrol(await Bekle(GrupAcikMi("Satış ve Satın Alma"), 15), "Acilan grup yeniden yuklemede hatirlandi");
        await Js(GrupTikla("Satış ve Satın Alma"));
        Kontrol(await Bekle($"!{GrupAcikMi("Satış ve Satın Alma")}", 5), "Grup kapatildi");

        await Git("/krediler");
        Kontrol(await Bekle(GrupAcikMi("Finans"), 15), "Bulunulan sayfanin grubu acik geliyor");
        Kontrol(await Js("document.querySelector('.mud-navmenu a.active').offsetWidth > 200") == "true", "Secili satir vurgusu menu genisliginde");
        await Ekran("3-finans");

        await GirisYap(calisan, "Sifre1234");
        Kontrol(await Bekle(Govde("Masraflarım"), 15), "Calisan Masraflarimi goruyor");
        await Task.Delay(500);
        var calisanBasliklari = await Js(Basliklar);
        Kontrol(!calisanBasliklari.Contains("Finans") && !calisanBasliklari.Contains("Stok ve Fiyat") && !calisanBasliklari.Contains("Personel"), "Yetkisiz gruplar ve tek maddelik grup basligi yok");
        Kontrol(await Js("document.querySelector('.menu-yakinda') === null") == "true", "Fatura gormeyen e-Belgeyi de gormuyor");
        await Ekran("4-calisan");
    }
}
