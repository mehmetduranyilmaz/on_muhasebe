using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OnMuhasebe.Modules.Stok.Contracts;
using OnMuhasebe.Modules.Stok.Domain;
using OnMuhasebe.Modules.Stok.Infrastructure;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Guvenlik;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;
using OnMuhasebe.Shared.Infrastructure.TopluAktarim;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Modules.Stok.Features;

// Excel'den toplu aktarım: şablon, önizleme (kayıt yapmaz) ve kayıt. Önizleme de ekleme yetkisi ister.
internal static class StokAktarimIslemleri
{
    public static void Map(RouteGroupBuilder grup)
    {
        var aktarim = grup.MapGroup("/aktarim").Yetki(StokYetkileri.Kart);
        aktarim.MapGet("/sablon", () => TypedResults.File(AktarimTablosu.Sablon("Stoklar", StokAktarimServisi.Sutunlar),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "stok_sablonu.xlsx")).Islem(YetkiIslemleri.Ekle);
        aktarim.MapPost("/onizleme", (IFormFile? dosya, DateOnly? acilisTarihi, StokAktarimServisi servis, CancellationToken ct) =>
            IsleAsync(dosya, acilisTarihi, false, servis, ct)).DisableAntiforgery().WithMetadata(new YuklemeBoyutu(GuvenlikExtensions.AzamiYuklemeBoyutu));
        aktarim.MapPost("", (IFormFile? dosya, DateOnly? acilisTarihi, StokAktarimServisi servis, CancellationToken ct) =>
            IsleAsync(dosya, acilisTarihi, true, servis, ct)).DisableAntiforgery().WithMetadata(new YuklemeBoyutu(GuvenlikExtensions.AzamiYuklemeBoyutu));
    }

    private static async Task<IResult> IsleAsync(IFormFile? dosya, DateOnly? acilisTarihi, bool kaydet, StokAktarimServisi servis, CancellationToken ct)
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
