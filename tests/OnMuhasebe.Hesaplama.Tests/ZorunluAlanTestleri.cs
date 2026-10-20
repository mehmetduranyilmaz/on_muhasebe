using FluentValidation;
using OnMuhasebe.Shared.Contracts.Ayarlar;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Hesaplama.Tests;

public class ZorunluAlanTestleri
{
    private sealed record Istek(string Ad, string? Telefon, int? Vade);

    private sealed class SabitSorgu(params string[] kodlar) : IZorunluAlanSorgusu
    {
        public Task<IReadOnlySet<string>> SeciliAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(kodlar.ToHashSet());
    }

    private sealed class Dogrulayici : AbstractValidator<Istek>
    {
        private static readonly ZorunluAlanTanimi[] _tanimlar = [new("t.telefon", "M", "Form", "Telefon"), new("t.vade", "M", "Form", "Vade günü")];

        public Dogrulayici(IZorunluAlanSorgusu sorgu)
        {
            RuleFor(x => x.Ad).NotEmpty();
            this.ZorunluAlanlar(sorgu, _tanimlar,
            [
                new AlanKontrolu<Istek>("t.telefon", nameof(Istek.Telefon), x => ZorunluAlanKurali.Dolu(x.Telefon)),
                new AlanKontrolu<Istek>("t.vade", nameof(Istek.Vade), x => x.Vade is not null)
            ]);
        }
    }

    [Fact]
    public async Task Secilmeyen_alan_bos_birakilabilir()
    {
        var sonuc = await new Dogrulayici(new SabitSorgu()).ValidateAsync(new Istek("A", null, null));
        Assert.True(sonuc.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Zorunlu_metin_alani_bos_ya_da_bosluksa_hata_verir(string? telefon)
    {
        var sonuc = await new Dogrulayici(new SabitSorgu("t.telefon")).ValidateAsync(new Istek("A", telefon, null));

        var hata = Assert.Single(sonuc.Errors);
        Assert.Equal((nameof(Istek.Telefon), "Telefon zorunludur (firma ayarı)."), (hata.PropertyName, hata.ErrorMessage));
    }

    [Fact]
    public async Task Yalnizca_secilen_alanlar_istenir_ve_dolu_olanlar_gecer()
    {
        var dogrulayici = new Dogrulayici(new SabitSorgu("t.vade", "baska.modul.alan"));

        Assert.Equal(["Vade"], (await dogrulayici.ValidateAsync(new Istek("A", null, null))).Errors.Select(e => e.PropertyName));
        Assert.True((await dogrulayici.ValidateAsync(new Istek("A", null, 30))).IsValid);
    }
}
