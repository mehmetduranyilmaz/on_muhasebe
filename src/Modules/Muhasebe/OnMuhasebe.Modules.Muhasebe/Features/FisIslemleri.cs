using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Modules.Muhasebe.Domain;
using OnMuhasebe.Modules.Muhasebe.Infrastructure;
using OnMuhasebe.Shared.Contracts.Sayfalama;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Lisans;
using OnMuhasebe.Shared.Infrastructure.Persistence;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.Validation;

namespace OnMuhasebe.Modules.Muhasebe.Features;

// Yevmiye fişleri: önizleme, dosya indirme ve dönemi "aktarıldı" işaretleme.
internal static class FisIslemleri
{
    // Mali müşavir aktarımı aylık/üç aylık yapar; bir yıl üst sınırı tek istekte sınırsız veri çekilmesini önler.
    private const int EnCokGun = 366;

    private static readonly Hata DonemHatasi =
        Hata.Dogrulama("donem", "Bitiş başlangıçtan önce olamaz ve dönem en çok bir yıl olabilir.");

    public static void Map(RouteGroupBuilder grup)
    {
        var aktarim = grup.MapGroup("").Yetki(MuhasebeYetkileri.Aktarim);
        aktarim.MapGet("/fisler", ListeleAsync);
        aktarim.MapGet("/fisler/indir", IndirAsync);
        aktarim.MapGet("/aktarimlar", AktarimlarAsync);
        aktarim.MapPost("/aktarimlar", AktarAsync).Dogrula<AktarimIstegi>();
        aktarim.MapPost("/aktarimlar/{id:guid}/iptal", IptalAsync);
    }

    internal sealed class AktarimDogrulayici : AbstractValidator<AktarimIstegi>
    {
        public AktarimDogrulayici()
        {
            RuleFor(x => x.Parmak).NotEmpty().MaximumLength(64);
            RuleFor(x => x.Aciklama).MaximumLength(500);
        }
    }

    private static bool DonemGecersiz(DateOnly baslangic, DateOnly bitis) => bitis < baslangic || bitis.DayNumber - baslangic.DayNumber >= EnCokGun;

    private static async Task<IResult> ListeleAsync(DateOnly baslangic, DateOnly bitis, Guid? isyeriId, FisServisi servis, CancellationToken ct) =>
        DonemGecersiz(baslangic, bitis) ? DonemHatasi.Problem() : TypedResults.Ok(await servis.ListeleAsync(baslangic, bitis, isyeriId, ct));

    // yalnizYeniler: aktarılmış ve değişmemiş fişler dosyaya girmez; muhasebe programına ikinci kez işlenmelerini önler.
    private static async Task<IResult> IndirAsync(
        DateOnly baslangic, DateOnly bitis, Guid? isyeriId, string? format, bool? yalnizYeniler, FisServisi servis, CancellationToken ct)
    {
        if (DonemGecersiz(baslangic, bitis))
        {
            return DonemHatasi.Problem();
        }

        var csvMi = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        if (!csvMi && format is not null && !string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Hata.Dogrulama("format", "Dosya biçimi xlsx ya da csv olmalıdır.").Problem();
        }

        var liste = await servis.ListeleAsync(baslangic, bitis, isyeriId, ct);
        var fisler = yalnizYeniler == true ? liste.Fisler.Where(f => f.AktarimDurumu != FisAktarimDurumu.Aktarildi).ToList() : liste.Fisler;
        var ad = $"yevmiye_{baslangic:yyyy-MM-dd}_{bitis:yyyy-MM-dd}";
        return csvMi
            ? TypedResults.File(FisDisaAktarici.Csv(fisler), "text/csv; charset=utf-8", $"{ad}.csv")
            : TypedResults.File(FisDisaAktarici.Excel(fisler), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{ad}.xlsx");
    }

    private static async Task<IResult> AktarimlarAsync(int? sayfa, int? boyut, MuhasebeDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Aktarimlar.AsNoTracking()
            .OrderByDescending(a => a.OlusturmaTarihi)
            .Select(a => new AktarimCevabi(a.Id, a.Baslangic, a.Bitis, a.IsyeriId, a.FisSayisi, a.ToplamBorc, a.Aciklama, a.AktaranAdi, a.OlusturmaTarihi, a.IptalTarihi))
            .SayfalaAsync(new SayfalamaIstegi(sayfa ?? 1, boyut ?? SayfalamaIstegi.VarsayilanBoyut), ct));

    // Parmak önizlemedeki listeyle eşleşmelidir: arada değişen belge, dosyaya girmediği halde aktarılmış sayılmasın.
    private static async Task<IResult> AktarAsync(
        AktarimIstegi istek, FisServisi servis, MuhasebeDbContext db, IMevcutKullanici mevcutKullanici, CancellationToken ct)
    {
        if (DonemGecersiz(istek.Baslangic, istek.Bitis))
        {
            return DonemHatasi.Problem();
        }

        var liste = await servis.ListeleAsync(istek.Baslangic, istek.Bitis, istek.IsyeriId, ct);
        if (liste.Parmak != istek.Parmak)
        {
            return Hata.Cakisma("liste_degisti", "Siz indirdikten sonra dönemin kayıtları değişti. Listeyi yenileyip dosyayı yeniden indirin.").Problem();
        }

        if (liste.Fisler.Any(f => !f.DengeliMi || FisServisi.KodsuzMu(f)))
        {
            return Hata.IsKurali("fis_hatali", "Dengesiz ya da hesap kodu boş fiş varken dönem aktarılmış sayılamaz. Uyarıları giderin.").Problem();
        }

        if (liste.Kalkanlar.Count == 0 && liste.Fisler.All(f => f.AktarimDurumu == FisAktarimDurumu.Aktarildi))
        {
            return Hata.IsKurali("degisiklik_yok", "Bu dönemde aktarılacak yeni ya da değişmiş fiş yok.").Problem();
        }

        var aktarim = MuhasebeAktarimi.Olustur(istek.Baslangic, istek.Bitis, istek.IsyeriId, liste.Fisler.Count, liste.Fisler.Sum(f => f.ToplamBorc),
            istek.Aciklama, mevcutKullanici.KullaniciAdi);
        db.Aktarimlar.Add(aktarim);
        db.AktarilanFisler.RemoveRange(await servis.DegisecekKayitlarAsync(liste, istek.IsyeriId, ct));
        db.AktarilanFisler.AddRange(liste.Fisler.Select(f => AktarilanFis.Olustur(aktarim.Id, f)));
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/muhasebe/aktarimlar/{aktarim.Id}", Cevap(aktarim));
    }

    // Fişler yeniden "aktarılmadı" görünür; önceki aktarımın kayıtları geri gelmez (yerini almışlardı).
    private static async Task<IResult> IptalAsync(Guid id, MuhasebeDbContext db, TimeProvider zaman, CancellationToken ct)
    {
        var aktarim = await db.Aktarimlar.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (aktarim is null)
        {
            return Hata.Bulunamadi("aktarim_bulunamadi", "Aktarım bulunamadı.").Problem();
        }

        if (aktarim.IptalTarihi is not null)
        {
            return Hata.IsKurali("aktarim_iptal", "Aktarım zaten iptal edilmiş.").Problem();
        }

        aktarim.IptalEt(zaman.GetUtcNow().UtcDateTime);
        db.AktarilanFisler.RemoveRange(await db.AktarilanFisler.IgnoreQueryFilters([ModulDbContext.IsyeriFiltresi]).Where(k => k.AktarimId == id).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(Cevap(aktarim));
    }

    private static AktarimCevabi Cevap(MuhasebeAktarimi a) =>
        new(a.Id, a.Baslangic, a.Bitis, a.IsyeriId, a.FisSayisi, a.ToplamBorc, a.Aciklama, a.AktaranAdi, a.OlusturmaTarihi, a.IptalTarihi);
}
