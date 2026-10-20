using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Web.Api;
using OnMuhasebe.Web.KasaBanka.Bilesenler;

namespace OnMuhasebe.Web.KasaBanka.Pages;

// Ekstre sihirbazı: dosya → (kolon eşleme) → satırları gözden geçir → sonuç. Aktarım sunucuda tutulduğu için yarıda bırakılan
// ekstreye "Önceki aktarımlar"dan dönülür.
public partial class EkstreAktarSayfasi
{
    private const int SayfaBoyutu = 50;

    private enum SatirFiltresi
    {
        Tumu,
        Aktarilacak,
        Eksik,
        Eslesen,
        Onceki,
        Atlanacak,
        Hatali,
        Aktarilan,
        Atlanan
    }

    private static readonly (SatirFiltresi Filtre, string Ad, string Renk)[] _filtreler =
    [
        (SatirFiltresi.Tumu, "Tümü", "#1E3A5F"),
        (SatirFiltresi.Aktarilacak, "Aktarılacak", "#14B8A6"),
        (SatirFiltresi.Eksik, "Seçim eksik", "#F59E0B"),
        (SatirFiltresi.Hatali, "Hatalı", "#DC2626"),
        (SatirFiltresi.Eslesen, "Kayıtlı hareketle eşleşen", "#3B82F6"),
        (SatirFiltresi.Onceki, "Daha önce aktarılan", "#64748B"),
        (SatirFiltresi.Atlanacak, "Atlanacak", "#64748B"),
        (SatirFiltresi.Aktarilan, "Aktarılan", "#16A34A"),
        (SatirFiltresi.Atlanan, "Atlanan", "#64748B")
    ];

    private readonly string[] _adimlar = ["Dosya", "Kolonlar", "Gözden geçir", "Sonuç"];

    [Parameter] public Guid Id { get; set; }
    [SupplyParameterFromQuery(Name = "aktarim")] public Guid? AktarimId { get; set; }

    private HesapDetayi? _hesap;
    private List<Kategori> _kategoriler = [];
    private List<HesapOzeti> _hesaplar = [];
    private List<AktarimOzeti>? _gecmis;
    private AktarimDetayi? _aktarim;
    private AktarimSonucu? _sonuc;
    private ApiHatasi? _hata;
    private ApiHatasi? _islemHatasi;
    private bool _surukleniyor;
    private bool _yukleniyor;

    private SatirFiltresi _filtre = SatirFiltresi.Tumu;
    private int _sayfa = 1;
    private readonly HashSet<Guid> _secili = [];

    private int _baslik;
    private int? _tarih;
    private int? _aciklama;
    private bool _tekKolon = true;
    private int? _tutar;
    private int? _alacak;
    private int? _borc;
    private int? _bakiye;
    private int? _referans;
    private bool _ters;
    private bool _sablon = true;

    private static string Adet(decimal d) => d.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);

    private int Adim => _aktarim is null ? 0 : _sonuc is not null ? 3 : _aktarim.Durum == AktarimDurumu.EslemeBekliyor ? 1 : 2;

    private bool EslemeTamamMi => _tarih is not null && _aciklama is not null && (_tekKolon ? _tutar is not null : _alacak is not null && _borc is not null);

    protected override async Task OnParametersSetAsync()
    {
        var hesap = await Api.GetAsync<HesapDetayi>($"api/kasabanka/hesaplar/{Id}");
        if (!hesap.BasariliMi)
        {
            _hata = hesap.Hata;
            return;
        }

        _hesap = hesap.Deger;
        var kategoriler = await Api.GetAsync<List<Kategori>>("api/kasabanka/kategoriler");
        var hesaplar = await Api.GetAsync<List<HesapOzeti>>("api/kasabanka/hesaplar");
        _kategoriler = kategoriler.BasariliMi ? kategoriler.Deger : [];
        _hesaplar = hesaplar.BasariliMi ? hesaplar.Deger : [];

        if (AktarimId is { } aktarimId)
        {
            await AktarimYukleAsync(aktarimId);
        }

        if (_aktarim is null)
        {
            await GecmisYukleAsync();
        }
    }

    private async Task GecmisYukleAsync()
    {
        var sonuc = await Api.GetAsync<List<AktarimOzeti>>($"api/kasabanka/hesaplar/{Id}/ekstre-aktarimlari");
        _gecmis = sonuc.BasariliMi ? sonuc.Deger : [];
    }

    private async Task AktarimYukleAsync(Guid aktarimId)
    {
        var sonuc = await Api.GetAsync<AktarimDetayi>($"api/kasabanka/ekstre-aktarimlari/{aktarimId}");
        if (sonuc.BasariliMi && sonuc.Deger.HesapId == Id)
        {
            AktarimAyarla(sonuc.Deger);
        }
        else if (!sonuc.BasariliMi)
        {
            Snackbar.Add(sonuc.Hata.Baslik, Severity.Error);
        }
    }

    private void AktarimAyarla(AktarimDetayi aktarim)
    {
        var ilkKez = _aktarim?.Id != aktarim.Id;
        _aktarim = aktarim;
        _islemHatasi = null;
        if (ilkKez)
        {
            _sonuc = null;
            _secili.Clear();
            _sayfa = 1;
            _filtre = SatirFiltresi.Tumu;
            EslemeyiDoldur(aktarim.Esleme);
        }

        _secili.IntersectWith(aktarim.Satirlar.Where(EkstreMetinleri.DuzenlenebilirMi).Select(s => s.Id));
    }

    private void EslemeyiDoldur(KolonEslemesi? e)
    {
        _baslik = Math.Max(0, e?.BaslikSatiri ?? 0);
        _tarih = e?.TarihKolonu;
        _aciklama = e?.AciklamaKolonu;
        _tekKolon = e?.TutarKolonu is not null || e?.AlacakKolonu is null;
        _tutar = e?.TutarKolonu;
        _alacak = e?.AlacakKolonu;
        _borc = e?.BorcKolonu;
        _bakiye = e?.BakiyeKolonu;
        _referans = e?.ReferansKolonu;
        _ters = e?.TutarTersMi ?? _hesap?.Tur == HesapTuru.KrediKarti;
    }

    // Önizleme tablosunda kolonların rengi ve rol adı.
    private Dictionary<int, (string? Ad, string? Renk)> KolonRolleri()
    {
        var roller = new Dictionary<int, (string? Ad, string? Renk)>();
        void Ekle(int? kolon, string ad, string renk)
        {
            if (kolon is { } k)
            {
                roller[k] = roller.TryGetValue(k, out var mevcut) ? ($"{mevcut.Ad} + {ad}", renk) : (ad, renk);
            }
        }

        Ekle(_tarih, "Tarih", "#3B82F6");
        Ekle(_aciklama, "Açıklama", "#8B5CF6");
        if (_tekKolon)
        {
            Ekle(_tutar, "Tutar", "#14B8A6");
        }
        else
        {
            Ekle(_alacak, "Giriş", "#16A34A");
            Ekle(_borc, "Çıkış", "#DC2626");
        }

        Ekle(_bakiye, "Bakiye", "#F59E0B");
        Ekle(_referans, "Referans", "#64748B");
        return roller;
    }

    private async Task DosyaSecildiAsync(InputFileChangeEventArgs e)
    {
        _surukleniyor = false;
        var dosya = e.File;
        if (dosya.Size > EkstreMetinleri.AzamiDosyaBoyutu)
        {
            Snackbar.Add("Ekstre dosyası en fazla 5 MB olabilir.", Severity.Error);
            return;
        }

        _yukleniyor = true;
        try
        {
            await using var akis = dosya.OpenReadStream(EkstreMetinleri.AzamiDosyaBoyutu);
            var sonuc = await Api.DosyaYukleAsync<AktarimDetayi>($"api/kasabanka/hesaplar/{Id}/ekstre-aktarimlari", "dosya", akis, dosya.Name);
            if (!sonuc.BasariliMi)
            {
                Snackbar.Add(sonuc.Hata.Baslik, Severity.Error);
                return;
            }

            AktarimAyarla(sonuc.Deger);
            if (sonuc.Deger.Durum == AktarimDurumu.OnayBekliyor)
            {
                Snackbar.Add($"{sonuc.Deger.Satirlar.Count} satır okundu.", Severity.Success);
            }
        }
        catch (IOException)
        {
            Snackbar.Add("Dosya okunamadı.", Severity.Error);
        }
        finally
        {
            _yukleniyor = false;
        }
    }

    private Task AktarimAcAsync(AktarimOzeti ozet) => AktarimYukleAsync(ozet.Id);

    private async Task AktarimSilAsync(AktarimOzeti ozet)
    {
        if (!await Onay.SorAsync("Aktarımı sil", $"\"{ozet.DosyaAdi}\" yüklemesi silinecek. Kayda geçmiş hareketler etkilenmez.", "Sil", tehlikeli: true))
        {
            return;
        }

        var sonuc = await Api.DeleteAsync($"api/kasabanka/ekstre-aktarimlari/{ozet.Id}");
        if (!sonuc.BasariliMi)
        {
            Snackbar.Add(sonuc.Hata.Baslik, Severity.Error);
            return;
        }

        await GecmisYukleAsync();
    }

    private async Task YeniDosyaAsync()
    {
        _aktarim = null;
        _sonuc = null;
        _gecmis = null;
        Navigasyon.NavigateTo($"kasabanka/{Id}/ekstre-aktar", replace: true);
        await GecmisYukleAsync();
    }

    private async Task EslemeUygulaAsync()
    {
        var esleme = new KolonEslemesi(_baslik, _tarih!.Value, _aciklama!.Value,
            _tekKolon ? _tutar : null, _tekKolon ? null : _alacak, _tekKolon ? null : _borc, _bakiye, _referans, _tekKolon && _ters);
        var sonuc = await Api.PutAsync<AktarimDetayi>($"api/kasabanka/ekstre-aktarimlari/{_aktarim!.Id}/esleme", new EslemeIstegi(esleme, _sablon));
        if (!sonuc.BasariliMi)
        {
            _islemHatasi = sonuc.Hata;
            return;
        }

        AktarimAyarla(sonuc.Deger);
        Snackbar.Add($"{sonuc.Deger.Satirlar.Count} satır okundu.", Severity.Success);
    }

    private static bool Uyar(EkstreSatiriDetayi s, SatirFiltresi filtre)
    {
        var bekliyor = s.Durum is EkstreSatirDurumu.Bekliyor;
        return filtre switch
        {
            SatirFiltresi.Aktarilacak => (bekliyor || s.Durum == EkstreSatirDurumu.Hatali) && s.IslemTuru != EkstreIslemTuru.Atla,
            SatirFiltresi.Eksik => bekliyor && s.IslemTuru != EkstreIslemTuru.Atla && EkstreMetinleri.Eksik(s) is not null,
            SatirFiltresi.Eslesen => s.Durum == EkstreSatirDurumu.ZatenKayitli,
            SatirFiltresi.Onceki => s.Durum == EkstreSatirDurumu.DahaOnceAktarildi,
            SatirFiltresi.Atlanacak => bekliyor && s.IslemTuru == EkstreIslemTuru.Atla,
            SatirFiltresi.Hatali => s.Durum == EkstreSatirDurumu.Hatali,
            SatirFiltresi.Aktarilan => s.Durum == EkstreSatirDurumu.Aktarildi,
            SatirFiltresi.Atlanan => s.Durum == EkstreSatirDurumu.Atlandi,
            _ => true
        };
    }

    private IEnumerable<EkstreSatiriDetayi> Filtreli(AktarimDetayi a) => a.Satirlar.Where(s => Uyar(s, _filtre));

    private static int EksikSayisi(AktarimDetayi a) =>
        a.Satirlar.Count(s => s.Durum is EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali && s.IslemTuru != EkstreIslemTuru.Atla && EkstreMetinleri.Eksik(s) is not null);

    private void FiltreSec(SatirFiltresi filtre)
    {
        _filtre = filtre;
        _sayfa = 1;
    }

    private void Sec(Guid id, bool secili)
    {
        if (secili)
        {
            _secili.Add(id);
        }
        else
        {
            _secili.Remove(id);
        }
    }

    private void SayfayiSec(IEnumerable<EkstreSatiriDetayi> satirlar, bool secili)
    {
        foreach (var s in satirlar.Where(EkstreMetinleri.DuzenlenebilirMi))
        {
            Sec(s.Id, secili);
        }
    }

    private string? Hedef(EkstreSatiriDetayi s) => s.IslemTuru switch
    {
        EkstreIslemTuru.Tahsilat or EkstreIslemTuru.Odeme => s.CariUnvan,
        EkstreIslemTuru.Gelir or EkstreIslemTuru.Gider => s.KategoriId is { } k ? _kategoriler.FirstOrDefault(x => x.Id == k)?.Ad : null,
        EkstreIslemTuru.Virman => s.KarsiHesapId is { } h ? _hesaplar.FirstOrDefault(x => x.Id == h)?.Ad : null,
        _ => null
    };

    private Task SatirDuzenleAsync(EkstreSatiriDetayi satir) =>
        EkstreMetinleri.DuzenlenebilirMi(satir) ? DuzenleAsync([satir]) : Task.CompletedTask;

    private Task SeciliDuzenleAsync() => DuzenleAsync(_aktarim!.Satirlar.Where(s => _secili.Contains(s.Id)).ToList());

    private async Task DuzenleAsync(IReadOnlyList<EkstreSatiriDetayi> satirlar)
    {
        var parametreler = new DialogParameters<SatirDuzenleDialog>
        {
            { d => d.AktarimId, _aktarim!.Id },
            { d => d.HesapId, Id },
            { d => d.ParaBirimi, _aktarim.ParaBirimi },
            { d => d.Satirlar, satirlar },
            { d => d.Kategoriler, _kategoriler },
            { d => d.Hesaplar, _hesaplar }
        };
        var baslik = satirlar.Count == 1 ? "Satırı düzenle" : $"{satirlar.Count} satırı düzenle";
        var dialog = await DialogService.ShowAsync<SatirDuzenleDialog>(baslik, parametreler, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        var sonuc = await dialog.Result;
        if (sonuc is { Canceled: false, Data: AktarimDetayi guncel })
        {
            AktarimAyarla(guncel);
            if (satirlar.Count > 1)
            {
                _secili.Clear();
            }
        }
        else if (satirlar.Count > 1)
        {
            await AktarimYukleAsync(_aktarim.Id);
        }
    }

    private async Task AktarAsync()
    {
        var a = _aktarim!;
        var yeni = a.Satirlar.Count(s => s.Durum is EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali && s.IslemTuru != EkstreIslemTuru.Atla);
        var eslesen = a.Satirlar.Count(s => s.Durum == EkstreSatirDurumu.ZatenKayitli);
        var atlanacak = a.Satirlar.Count(s => s.Durum is EkstreSatirDurumu.Bekliyor or EkstreSatirDurumu.Hatali && s.IslemTuru == EkstreIslemTuru.Atla);
        var eksik = EksikSayisi(a);
        string?[] parcalar =
        [
            $"{yeni} satır {a.HesapAdi} hesabına hareket olarak kaydedilecek.",
            eslesen > 0 ? $"{eslesen} satır elle girilmiş hareketlere bağlanacak." : null,
            atlanacak > 0 ? $"{atlanacak} satır atlanacak." : null,
            eksik > 0 ? $"Seçimi eksik {eksik} satır aktarılamayıp hatalı işaretlenecek." : null
        ];
        if (!await Onay.SorAsync("Ekstreyi aktar", string.Join(" ", parcalar.Where(p => p is not null)), "Aktar"))
        {
            return;
        }

        var sonuc = await Api.PostAsync<AktarimSonucu>($"api/kasabanka/ekstre-aktarimlari/{a.Id}/aktar", new { });
        if (!sonuc.BasariliMi)
        {
            Snackbar.Add(sonuc.Hata.Baslik, Severity.Error);
            return;
        }

        AktarimAyarla(sonuc.Deger.Aktarim);
        _sonuc = sonuc.Deger;
    }

    private void HatalilariDuzelt()
    {
        _sonuc = null;
        _filtre = SatirFiltresi.Hatali;
        _sayfa = 1;
    }
}
