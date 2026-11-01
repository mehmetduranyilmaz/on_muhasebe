using OnMuhasebe.Shared.Domain;

namespace OnMuhasebe.Hesaplama.Tests;

// Sunucu UTC'de çalışır: Türkiye'de gece 00.00-03.00 arası UTC tarihi bir gün geridedir.
public class TurkiyeSaatiTestleri
{
    private sealed class SabitZaman(DateTimeOffset an) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => an;
    }

    [Theory]
    [InlineData("2026-11-01T20:59:59Z", "2026-11-01")]
    [InlineData("2026-11-01T21:00:00Z", "2026-11-02")]
    [InlineData("2026-12-31T21:30:00Z", "2027-01-01")]
    public void Bugun_turkiye_gunudur(string utc, string beklenen) =>
        Assert.Equal(DateOnly.Parse(beklenen), TurkiyeSaati.Bugun(new SabitZaman(DateTimeOffset.Parse(utc))));

    [Theory]
    [InlineData("2026-11-01T21:30:00Z", "2026-11-02")]
    [InlineData("2026-11-01T20:30:00Z", "2026-11-01")]
    public void Utc_anin_gunu(string utc, string beklenen) =>
        Assert.Equal(DateOnly.Parse(beklenen), TurkiyeSaati.Gunu(DateTimeOffset.Parse(utc).UtcDateTime));

    [Fact]
    public void Gun_basi_utc_uc_saat_geridedir() =>
        Assert.Equal(new DateTime(2026, 11, 1, 21, 0, 0, DateTimeKind.Utc), TurkiyeSaati.GunBasiUtc(new DateOnly(2026, 11, 2)));
}
