using OnMuhasebe.Shared.Contracts.Isyerleri;
using OnMuhasebe.Shared.Infrastructure.Kimlik;

namespace OnMuhasebe.Hesaplama.Tests;

public class IsyeriSeciciTestleri
{
    private static readonly Guid _firma = Guid.CreateVersion7();
    private static readonly IsyeriOzeti _merkez = Isyeri("MRK", merkez: true);
    private static readonly IsyeriOzeti _sube = Isyeri("ANK");
    private static readonly IsyeriOzeti _kapali = Isyeri("IZM", aktif: false);

    private static IsyeriOzeti Isyeri(string kod, bool merkez = false, bool aktif = true) =>
        new(Guid.CreateVersion7(), kod, kod, merkez, aktif, null, null, null, null, null);

    private sealed class Sorgu : IIsyeriSorgusu
    {
        public Task<IReadOnlyList<IsyeriOzeti>> FirmaninAsync(Guid firmaId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IsyeriOzeti>>([_merkez, _sube, _kapali]);

        public Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Denetim(KullaniciYetkileri yetkiler) : IYetkiDenetimi
    {
        public Task<bool> VarMiAsync(string yetki, CancellationToken cancellationToken = default) => Task.FromResult(yetkiler.VarMi(yetki));

        public Task<KullaniciYetkileri> GetirAsync(CancellationToken cancellationToken = default) => Task.FromResult(yetkiler);
    }

    private sealed class Kullanici : IMevcutKullanici
    {
        public Guid? KullaniciId => Guid.Empty;
        public Guid? FirmaId => _firma;
        public string? KullaniciAdi => null;
        public bool SistemYoneticisiMi => false;
        public List<Guid>? IzinliIsyerleri => null;
    }

    private static IsyeriSecici Secici(IReadOnlyList<Guid> izinli, Guid? varsayilan = null) =>
        new(new Sorgu(), new Denetim(new KullaniciYetkileri(false, [], izinli, varsayilan)), new Kullanici());

    [Fact]
    public async Task Bos_istekte_varsayilan_yoksa_merkez_secilir() =>
        Assert.Equal(_merkez.Id, (await Secici([]).BelirleAsync(null)).Deger);

    [Fact]
    public async Task Bos_istekte_kullanicinin_varsayilani_secilir() =>
        Assert.Equal(_sube.Id, (await Secici([], _sube.Id).BelirleAsync(null)).Deger);

    [Fact]
    public async Task Merkeze_giremeyen_kullaniciya_izinli_ilk_isyeri_secilir() =>
        Assert.Equal(_sube.Id, (await Secici([_sube.Id]).BelirleAsync(null)).Deger);

    [Fact]
    public async Task Izinsiz_isyerinde_kayit_acilamaz() =>
        Assert.Equal(IsyeriHatalari.Yetkisiz, (await Secici([_sube.Id]).BelirleAsync(_merkez.Id)).Hata);

    [Fact]
    public async Task Kapali_isyerinde_yeni_kayit_acilamaz_ama_mevcut_kayit_korunur()
    {
        Assert.Equal(IsyeriHatalari.Pasif, (await Secici([]).BelirleAsync(_kapali.Id)).Hata);
        Assert.Equal(_kapali.Id, (await Secici([]).DegisiklikAsync(_kapali.Id, _kapali.Id)).Deger);
    }

    [Fact]
    public async Task Baska_firmanin_isyeri_bulunamaz() =>
        Assert.Equal(IsyeriHatalari.Bulunamadi, (await Secici([]).BelirleAsync(Guid.CreateVersion7())).Hata);

    [Fact]
    public async Task Girilebilir_aktif_isyeri_yoksa_hata() =>
        Assert.Equal(IsyeriHatalari.Yok, (await Secici([_kapali.Id]).BelirleAsync(null)).Hata);
}
