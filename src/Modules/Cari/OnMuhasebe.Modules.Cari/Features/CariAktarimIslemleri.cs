using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Modules.Cari.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Guvenlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.TopluAktarim;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Cari.Features;

// Excel'den toplu aktarım: şablon, önizleme (kayıt yapmaz) ve kayıt. Önizleme de ekleme yetkisi ister.
internal static class CariAktarimIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var aktarim = grup.MapGroup("/aktarim").Yetki(CariYetkileri.Hesap);
        aktarim.MapGet("/sablon", () => TypedResults.File(AktarimTablosu.Sablon("Cariler", CariAktarimServisi.Sutunlar),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "cari_sablonu.xlsx")).Islem(YetkiIslemleri.Ekle);
        aktarim.MapPost("/onizleme", (IFormFile? dosya, DateOnly? acilisTarihi, CariAktarimServisi servis, CancellationToken ct) =>
            IsleAsync(dosya, acilisTarihi, false, servis, ct)).DisableAntiforgery().WithMetadata(new YuklemeBoyutu(GuvenlikExtensions.AzamiYuklemeBoyutu));
        aktarim.MapPost("", (IFormFile? dosya, DateOnly? acilisTarihi, CariAktarimServisi servis, CancellationToken ct) =>
            IsleAsync(dosya, acilisTarihi, true, servis, ct)).DisableAntiforgery().WithMetadata(new YuklemeBoyutu(GuvenlikExtensions.AzamiYuklemeBoyutu));
    }

    private static async Task<IResult> IsleAsync(IFormFile? dosya, DateOnly? acilisTarihi, bool kaydet, CariAktarimServisi servis, CancellationToken ct)
    {
        if (dosya is null || dosya.Length == 0)
        {
            return Hata.Dogrulama("dosya_okunamadi", "Excel dosyası seçin.").Problem();
        }

        if (dosya.Length > AktarimTablosu.AzamiDosyaBoyutu)
        {
            return Hata.Dogrulama("dosya_cok_buyuk", "Dosya en fazla 5 MB olabilir.").Problem();
        }

        using var bellek = new MemoryStream();
        await dosya.CopyToAsync(bellek, ct);
        var sonuc = await servis.IsleAsync(bellek.ToArray(), acilisTarihi, kaydet, ct);
        return sonuc.BasariliMi ? TypedResults.Ok(sonuc.Deger) : sonuc.Hata.Problem();
    }
}
