using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Modules.KasaBanka.Domain;
using OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure;

internal sealed class KasaBankaDbContext(DbContextOptions<KasaBankaDbContext> options, IMevcutKullanici mevcutKullanici)
    : ModulDbContext(options, mevcutKullanici)
{
    public const string SemaAdi = "kasabanka";

    protected override string Sema => SemaAdi;

    public DbSet<KasaHesabi> Hesaplar => Set<KasaHesabi>();
    public DbSet<KasaHareketi> KasaHareketleri => Set<KasaHareketi>();
    public DbSet<GelirGiderKaydi> GelirGiderKayitlari => Set<GelirGiderKaydi>();
    public DbSet<GelirGiderKategorisi> Kategoriler => Set<GelirGiderKategorisi>();
    public DbSet<EkstreAktarimi> EkstreAktarimlari => Set<EkstreAktarimi>();
    public DbSet<EkstreSatiri> EkstreSatirlari => Set<EkstreSatiri>();
    public DbSet<EkstreSablonu> EkstreSablonlari => Set<EkstreSablonu>();
    public DbSet<EkstreKurali> EkstreKurallari => Set<EkstreKurali>();
    public DbSet<Evrak> Evraklar => Set<Evrak>();
    public DbSet<EvrakHareketi> EvrakHareketleri => Set<EvrakHareketi>();
    public DbSet<EvrakBordrosu> EvrakBordrolari => Set<EvrakBordrosu>();
    public DbSet<EvrakBorclusu> EvrakBorclulari => Set<EvrakBorclusu>();
    public DbSet<BorcluHesabi> BorcluHesaplari => Set<BorcluHesabi>();
    public DbSet<CekSenetAyari> CekSenetAyarlari => Set<CekSenetAyari>();
    public DbSet<EvrakKarnesi> Karneler => Set<EvrakKarnesi>();
    public DbSet<Teminat> Teminatlar => Set<Teminat>();
    public DbSet<Sube> Subeler => Set<Sube>();
    public DbSet<Kredi> Krediler => Set<Kredi>();
    public DbSet<KrediTaksiti> KrediTaksitleri => Set<KrediTaksiti>();

    // Kolon eşlemesi JSON olarak tek kolonda tutulur; konvansiyonla tanımlanınca EF onu ilişkili entity sanmaz.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<KolonEslemesi>().HaveConversion<KolonEslemesiDonusturucu>().HaveColumnType("jsonb");
    }

    private sealed class KolonEslemesiDonusturucu() : ValueConverter<KolonEslemesi, string>(
        v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
        v => JsonSerializer.Deserialize<KolonEslemesi>(v, JsonSerializerOptions.Default)!);
}
