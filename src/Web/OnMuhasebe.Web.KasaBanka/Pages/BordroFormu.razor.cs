using Microsoft.AspNetCore.Components;
using MudBlazor;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Contracts.Doviz;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Web.Api;
using OnMuhasebe.Web.KasaBanka.Bilesenler;

namespace OnMuhasebe.Web.KasaBanka.Pages;

// Giriş (alınan) ve çıkış (verilen) bordrosu. Riskli keşideci uyarısı kart üzerinde anlık gösterilir; kayıtta API aynı
// kontrolü yapar ve onay isterse kullanıcıya sorulup onayla yeniden gönderilir.
public partial class BordroFormu
{
    private sealed class Satir
    {
        public EvrakTuru Tur { get; set; }
        public string? SeriNo { get; set; }
        public DateTime? Vade { get; set; }
        public decimal Tutar { get; set; }
        public string ParaBirimi { get; set; } = ParaBirimleri.Tl;
        public BorcluOzeti? Borclu { get; set; }
        public bool YeniBorclu { get; set; }
        public string? BorcluAdi { get; set; }
        public string? KimlikNo { get; set; }
        public List<BorcluHesapBilgisi> KayitliHesaplar { get; set; } = [];
        public string? BankaAdi { get; set; }
        public string? Sube { get; set; }
        public string? HesapNo { get; set; }
        public string? KesideYeri { get; set; }
        public string? KefilAdi { get; set; }
        public string? KefilKimlikNo { get; set; }
        public Guid? KendiHesapId { get; set; }
        public Guid? KarneId { get; set; }
        public string? Aciklama { get; set; }
        public RiskBilgisi? Risk { get; set; }
    }

    [SupplyParameterFromQuery(Name = "cari")] public Guid? CariParametresi { get; set; }
    [SupplyParameterFromQuery(Name = "donus")] public string? Donus { get; set; }

    private CariSecim? _cari;
    private string? _cariVergiNo;
    private RiskBilgisi? _cariRiski;
    private DateTime? _tarih = DateTime.Today;
    private string? _aciklama;
    private readonly List<Satir> _satirlar = [];
    private List<HesapOzeti> _hesaplar = [];
    private List<KarneOzeti> _karneler = [];
    private readonly Dictionary<Guid, KarneDetayi> _karneDetaylari = [];
    private CekSenetAyarlari _ayar = new(true, false, RiskDavranisi.UyarVeOnayIste);
    private ApiHatasi? _hata;
    private readonly IslemAnahtari _anahtar = new();
    private IReadOnlySet<string> _zorunlu = new HashSet<string>();

    private bool Z(string kod) => _zorunlu.Contains(kod);

    private bool Alinan => Navigasyon.ToBaseRelativePath(Navigasyon.Uri).StartsWith("cek-senet/giris", StringComparison.Ordinal);

    private bool KimlikZorunlu(Satir s) => s.Tur == EvrakTuru.Cek ? _ayar.KesideciKimlikZorunlu : _ayar.SenetBorcluKimlikZorunlu;

    private string ParaBirimiOf(Satir s) =>
        !Alinan && s.Tur == EvrakTuru.Cek ? _hesaplar.FirstOrDefault(h => h.Id == s.KendiHesapId)?.ParaBirimi ?? ParaBirimleri.Tl : s.ParaBirimi;

    // Tutar ağırlıklı ortalama vade (yalnızca tek dövizli bordroda anlamlı).
    private DateOnly? OrtalamaVade
    {
        get
        {
            var gecerli = _satirlar.Where(s => s.Tutar > 0 && s.Vade is not null).ToList();
            if (gecerli.Count == 0 || gecerli.Select(ParaBirimiOf).Distinct().Count() > 1)
            {
                return null;
            }

            var gun = gecerli.Sum(s => (decimal)DateOnly.FromDateTime(s.Vade!.Value).DayNumber * s.Tutar) / gecerli.Sum(s => s.Tutar);
            return DateOnly.FromDayNumber((int)Math.Round(gun));
        }
    }

    private string? Eksik
    {
        get
        {
            if (_cari is null)
            {
                return "Cari seçin.";
            }

            if (_satirlar.Count == 0)
            {
                return "En az bir evrak ekleyin.";
            }

            if (Z(KasaBankaZorunluAlanlari.BordroAciklama) && string.IsNullOrWhiteSpace(_aciklama))
            {
                return "Bordro açıklaması zorunlu (firma ayarı).";
            }

            foreach (var (s, i) in _satirlar.Select((s, i) => (s, i + 1)))
            {
                var eksik = string.IsNullOrWhiteSpace(s.SeriNo) ? s.KarneId is null ? "evrak no" : "evrak no (karnede boş yaprak yok)"
                    : s.Vade is null ? "vade"
                    : s.Tutar <= 0 ? "tutar"
                    : Alinan && s.YeniBorclu && string.IsNullOrWhiteSpace(s.BorcluAdi) ? "keşideci/borçlu adı"
                    : Alinan && !s.YeniBorclu && s.Borclu is null ? "keşideci/borçlu"
                    : Alinan && s.YeniBorclu && KimlikZorunlu(s) && string.IsNullOrWhiteSpace(s.KimlikNo) ? "VKN/TCKN"
                    : Alinan && s.Tur == EvrakTuru.Cek && string.IsNullOrWhiteSpace(s.BankaAdi) ? "banka"
                    : Alinan && s.Tur == EvrakTuru.Senet && Z(KasaBankaZorunluAlanlari.SenetKesideYeri) && string.IsNullOrWhiteSpace(s.KesideYeri) ? "keşide yeri"
                    : Alinan && s.Tur == EvrakTuru.Senet && Z(KasaBankaZorunluAlanlari.SenetKefil) && string.IsNullOrWhiteSpace(s.KefilAdi) ? "kefil"
                    : !Alinan && s.Tur == EvrakTuru.Cek && s.KendiHesapId is null ? "banka hesabı"
                    : null;
                if (eksik is not null)
                {
                    return $"{i}. evrakta {eksik} eksik.";
                }
            }

            return null;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _zorunlu = await ZorunluAlanlar.SeciliAsync();
        var hesaplar = await Api.GetAsync<List<HesapOzeti>>("api/kasabanka/hesaplar");
        _hesaplar = hesaplar.BasariliMi ? hesaplar.Deger : [];
        if (!Alinan)
        {
            var karneler = await Api.GetAsync<List<KarneOzeti>>("api/kasabanka/cek-senet/karneler?aktif=true");
            _karneler = karneler.BasariliMi ? karneler.Deger : [];
        }

        var ayar = await Api.GetAsync<CekSenetAyarlari>("api/kasabanka/cek-senet/ayarlar");
        if (ayar.BasariliMi)
        {
            _ayar = ayar.Deger;
        }

        if (CariParametresi is { } cariId && await Api.GetAsync<CariDetayi>($"api/cari/hesaplar/{cariId}") is { BasariliMi: true } c)
        {
            await CariSecildiAsync(new CariSecim(c.Deger.Id, c.Deger.Unvan, c.Deger.Kod));
        }

        await EkleAsync(EvrakTuru.Cek);
    }

    private async Task EkleAsync(EvrakTuru tur)
    {
        var onceki = _satirlar.LastOrDefault();
        var satir = new Satir
        {
            Tur = tur,
            Vade = onceki?.Vade,
            ParaBirimi = onceki?.ParaBirimi ?? ParaBirimleri.Tl,
            KendiHesapId = onceki?.KendiHesapId ?? _hesaplar.FirstOrDefault(h => h.Tur == HesapTuru.Banka && h.AktifMi)?.Id
        };
        _satirlar.Add(satir);
        await KarneVarsayilaniAsync(satir);
    }

    // Aynı keşidecinin ardışık çekleri için: bilgiler kopyalanır, çek no bir artırılır.
    private async Task KopyalaAsync(Satir s)
    {
        var yeni = Klon(s, EvrakSeriNo.Ardisik(s.SeriNo ?? "", 2) is { } ardisik ? ardisik[1] : null, s.Vade?.AddMonths(1), s.Tutar);
        _satirlar.Insert(_satirlar.IndexOf(s) + 1, yeni);
        if (yeni.KarneId is not null)
        {
            await SiradakiYaprakAsync(yeni);
        }
    }

    // Seri giriş: satır planın ilk evrakı olur, kalanlar aynı bilgilerle arkasına eklenir. Karnede numaralar sırayla karneden gelir.
    private async Task SeriEkleAsync(Satir s)
    {
        var parametreler = new DialogParameters<SeriGirisDialog>
        {
            { x => x.Tur, s.Tur },
            { x => x.IlkSeri, s.SeriNo },
            { x => x.IlkVade, s.Vade },
            { x => x.Tutar, s.Tutar },
            { x => x.ParaBirimi, ParaBirimiOf(s) },
            { x => x.Karneli, !Alinan && s.KarneId is not null }
        };
        var dialog = await DialogService.ShowAsync<SeriGirisDialog>($"Seri {CekSenetMetinleri.Tur(s.Tur).ToLower(Bicim.Kultur)} girişi", parametreler,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        if (await dialog.Result is not { Canceled: false, Data: IReadOnlyList<SeriGirisDialog.SeriSatiri> plan })
        {
            return;
        }

        var ilk = plan[0];
        (s.Vade, s.Tutar) = (ilk.Vade.ToDateTime(TimeOnly.MinValue), ilk.Tutar);
        if (ilk.Seri is not null)
        {
            s.SeriNo = ilk.Seri;
        }

        var sira = _satirlar.IndexOf(s);
        foreach (var p in plan.Skip(1))
        {
            var yeni = Klon(s, p.Seri, p.Vade.ToDateTime(TimeOnly.MinValue), p.Tutar);
            _satirlar.Insert(++sira, yeni);
            if (yeni.KarneId is not null)
            {
                await SiradakiYaprakAsync(yeni);
            }
        }

        Snackbar.Add($"{plan.Count} evrak hazırlandı; kaydetmeden önce kontrol edin.", Severity.Info);
    }

    private static Satir Klon(Satir s, string? seriNo, DateTime? vade, decimal tutar) => new()
    {
        Tur = s.Tur, Vade = vade, Tutar = tutar, ParaBirimi = s.ParaBirimi, Borclu = s.Borclu, YeniBorclu = s.YeniBorclu,
        BorcluAdi = s.BorcluAdi, KimlikNo = s.KimlikNo, KayitliHesaplar = s.KayitliHesaplar, BankaAdi = s.BankaAdi, Sube = s.Sube,
        HesapNo = s.HesapNo, KesideYeri = s.KesideYeri, KefilAdi = s.KefilAdi, KefilKimlikNo = s.KefilKimlikNo, KendiHesapId = s.KendiHesapId,
        Risk = s.Risk, KarneId = s.KarneId, SeriNo = seriNo
    };

    // Bu bordrodaki satırlar da düşüldükten sonra karnede kalacak boş yaprak eşiğin altındaysa seri alanının altında uyarı.
    private string? KarneIpucu(Satir s)
    {
        if (Alinan || s.KarneId is not { } id || _karneler.FirstOrDefault(k => k.Id == id) is not { } k)
        {
            return null;
        }

        var kalacak = k.Kalan - _satirlar.Count(x => x.KarneId == id && !string.IsNullOrWhiteSpace(x.SeriNo));
        return _ayar.KarneUyariEsigi > 0 && kalacak <= _ayar.KarneUyariEsigi ? $"Bu bordrodan sonra karnede {kalacak} boş yaprak kalacak" : null;
    }

    // Verilen evrakta kullanılabilecek karneler: çekte seçili banka hesabınınkiler, senette senet defterleri.
    private IEnumerable<KarneOzeti> Karneleri(Satir s) =>
        _karneler.Where(k => k.Tur == s.Tur && (s.Tur == EvrakTuru.Senet || k.KendiHesapId == s.KendiHesapId));

    private async Task KarneVarsayilaniAsync(Satir s)
    {
        if (Alinan)
        {
            return;
        }

        var uygun = Karneleri(s).ToList();
        if (s.KarneId is null || uygun.All(k => k.Id != s.KarneId))
        {
            s.KarneId = uygun.FirstOrDefault(k => k.Kalan > 0)?.Id;
        }

        await SiradakiYaprakAsync(s);
    }

    private async Task HesapDegistiAsync(Satir s, Guid? hesapId)
    {
        s.KendiHesapId = hesapId;
        s.KarneId = null;
        s.SeriNo = null;
        await KarneVarsayilaniAsync(s);
    }

    private async Task KarneSecildiAsync(Satir s, Guid? karneId)
    {
        s.KarneId = karneId;
        s.SeriNo = null;
        await SiradakiYaprakAsync(s);
    }

    // Karnenin ilk boş yaprağı; aynı bordroda aynı karneden önceki satırların aldığı yapraklar atlanır.
    private async Task SiradakiYaprakAsync(Satir s)
    {
        if (s.KarneId is not { } karneId)
        {
            return;
        }

        if (!_karneDetaylari.TryGetValue(karneId, out var detay))
        {
            var sonuc = await Api.GetAsync<KarneDetayi>($"api/kasabanka/cek-senet/karneler/{karneId}");
            if (!sonuc.BasariliMi)
            {
                return;
            }

            detay = _karneDetaylari[karneId] = sonuc.Deger;
        }

        var alinmis = _satirlar.Where(x => x != s && x.KarneId == karneId && x.SeriNo is not null).Select(x => EvrakSeriNo.Normalize(x.SeriNo!)).ToHashSet();
        s.SeriNo = detay.Yapraklar.FirstOrDefault(y => y.Durum == YaprakDurumu.Bos && !alinmis.Contains(y.SeriNo))?.SeriNo;
    }

    private async Task<IEnumerable<CariSecim>> CariAraAsync(string? arama, CancellationToken cancellationToken)
    {
        var sonuc = await Api.GetAsync<SayfaliSonuc<CariOzeti>>(
            $"api/cari/hesaplar?aktif=true&boyut=10&arama={Uri.EscapeDataString(arama ?? "")}", cancellationToken);
        return sonuc.BasariliMi ? sonuc.Deger.Kayitlar.Select(c => new CariSecim(c.Id, c.Unvan, c.Kod)) : [];
    }

    private async Task CariSecildiAsync(CariSecim? cari)
    {
        _cari = cari;
        _cariVergiNo = null;
        _cariRiski = null;
        if (cari is null)
        {
            return;
        }

        if (await Api.GetAsync<CariDetayi>($"api/cari/hesaplar/{cari.Id}") is { BasariliMi: true } d)
        {
            _cariVergiNo = d.Deger.VergiNo;
        }

        if (Alinan)
        {
            var risk = await Api.GetAsync<RiskBilgisi>($"api/kasabanka/cek-senet/risk?cariId={cari.Id}");
            _cariRiski = risk.BasariliMi ? risk.Deger : null;
        }
    }

    private async Task<IEnumerable<BorcluOzeti>> BorcluAraAsync(string? arama, CancellationToken cancellationToken)
    {
        var sonuc = await Api.GetAsync<SayfaliSonuc<BorcluOzeti>>(
            $"api/kasabanka/cek-senet/borclular?boyut=10&arama={Uri.EscapeDataString(arama ?? "")}", cancellationToken);
        return sonuc.BasariliMi ? sonuc.Deger.Kayitlar : [];
    }

    private async Task BorcluSecildiAsync(Satir s, BorcluOzeti? borclu)
    {
        s.Borclu = borclu;
        s.KayitliHesaplar = [];
        s.Risk = null;
        if (borclu is null)
        {
            return;
        }

        if (await Api.GetAsync<BorcluDetayi>($"api/kasabanka/cek-senet/borclular/{borclu.Id}") is { BasariliMi: true } d)
        {
            s.KayitliHesaplar = d.Deger.Hesaplar.ToList();
            if (s.KayitliHesaplar.Count == 1 && string.IsNullOrWhiteSpace(s.BankaAdi))
            {
                (s.BankaAdi, s.Sube, s.HesapNo) = (s.KayitliHesaplar[0].BankaAdi, s.KayitliHesaplar[0].Sube, s.KayitliHesaplar[0].HesapNo);
            }
        }

        await RiskKontrolAsync(s, $"borcluId={borclu.Id}");
    }

    private static void YeniBorcluAc(Satir s)
    {
        s.YeniBorclu = true;
        s.Borclu = null;
        s.KayitliHesaplar = [];
        s.Risk = null;
    }

    // Kimlik no tamamlanınca kayıtlı kart varsa ona geçilir (aynı kişi iki kez açılmaz); yoksa risk yine numarayla sorgulanır.
    private async Task KimlikDegistiAsync(Satir s, string? kimlik)
    {
        s.KimlikNo = kimlik?.Trim();
        s.Risk = null;
        if (s.KimlikNo is not { Length: 10 or 11 } no)
        {
            return;
        }

        var bulunan = (await BorcluAraAsync(no, CancellationToken.None)).FirstOrDefault(b => b.KimlikNo == no);
        if (bulunan is not null)
        {
            s.YeniBorclu = false;
            Snackbar.Add($"Kayıtlı keşideci/borçlu bulundu: {bulunan.Ad}", Severity.Info);
            await BorcluSecildiAsync(s, bulunan);
            return;
        }

        await RiskKontrolAsync(s, $"kimlikNo={no}");
    }

    private async Task CariyiBorcluYapAsync(Satir s)
    {
        YeniBorcluAc(s);
        s.BorcluAdi = _cari!.Unvan;
        await KimlikDegistiAsync(s, _cariVergiNo);
    }

    private async Task RiskKontrolAsync(Satir s, string sorgu)
    {
        var sonuc = await Api.GetAsync<RiskBilgisi>($"api/kasabanka/cek-senet/risk?{sorgu}");
        s.Risk = sonuc.BasariliMi ? sonuc.Deger : null;
    }

    private async Task KaydetAsync()
    {
        _hata = null;
        var sonuc = Alinan ? await GirisAsync(false) : await CikisAsync();
        if (!sonuc.BasariliMi && sonuc.Hata.Kod == "risk_onayi_gerekli")
        {
            if (!await Onay.SorAsync("Riskli keşideci / borçlu", sonuc.Hata.Baslik, "Yine de kabul et", tehlikeli: true))
            {
                return;
            }

            sonuc = await GirisAsync(true);
        }

        _anahtar.Sonuc(sonuc.Hata);

        if (!sonuc.BasariliMi)
        {
            _hata = sonuc.Hata;
            return;
        }

        Snackbar.Add($"Bordro {sonuc.Deger.BordroNo} kaydedildi.", Severity.Success);
        Navigasyon.NavigateTo($"cek-senet/bordro/{sonuc.Deger.BordroId}");
    }

    private Task<ApiSonucu<BordroSonucu>> GirisAsync(bool riskOnaylandi) =>
        Api.PostAsync<BordroSonucu>("api/kasabanka/cek-senet/giris", new AlinanBordroIstegi(
            _cari!.Id, DateOnly.FromDateTime(_tarih!.Value), Bos(_aciklama), riskOnaylandi,
            _satirlar.Select(s => new AlinanEvrakGirdisi(
                s.Tur, s.SeriNo!.Trim(), DateOnly.FromDateTime(s.Vade!.Value), s.Tutar, s.ParaBirimi, null, null,
                s.YeniBorclu ? new BorcluGirdisi(null, Bos(s.BorcluAdi), Bos(s.KimlikNo), null) : new BorcluGirdisi(s.Borclu!.Id, null, null, null),
                s.Tur == EvrakTuru.Cek ? Bos(s.BankaAdi) : null, s.Tur == EvrakTuru.Cek ? Bos(s.Sube) : null, s.Tur == EvrakTuru.Cek ? Bos(s.HesapNo) : null,
                s.Tur == EvrakTuru.Senet ? Bos(s.KesideYeri) : null, s.Tur == EvrakTuru.Senet ? Bos(s.KefilAdi) : null,
                s.Tur == EvrakTuru.Senet ? Bos(s.KefilKimlikNo) : null, Bos(s.Aciklama))).ToList()), islemAnahtari: _anahtar.Deger);

    private Task<ApiSonucu<BordroSonucu>> CikisAsync() =>
        Api.PostAsync<BordroSonucu>("api/kasabanka/cek-senet/cikis", new VerilenBordroIstegi(
            _cari!.Id, DateOnly.FromDateTime(_tarih!.Value), Bos(_aciklama),
            _satirlar.Select(s => new VerilenEvrakGirdisi(
                s.Tur, s.SeriNo!.Trim(), DateOnly.FromDateTime(s.Vade!.Value), s.Tutar, s.Tur == EvrakTuru.Senet ? s.ParaBirimi : null,
                s.Tur == EvrakTuru.Cek ? s.KendiHesapId : null, null, null, Bos(s.Aciklama), s.KarneId)).ToList()), islemAnahtari: _anahtar.Deger);

    private static string? Bos(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
}
