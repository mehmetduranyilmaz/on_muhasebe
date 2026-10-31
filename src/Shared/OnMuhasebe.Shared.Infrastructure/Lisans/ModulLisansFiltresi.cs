using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Kimlik;
using OnMuhasebe.Shared.Infrastructure.Results;

namespace OnMuhasebe.Shared.Infrastructure.Lisans;

internal sealed class ModulLisansFiltresi(string modulKodu) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var firmaId = http.RequestServices.GetRequiredService<IMevcutKullanici>().FirmaId;
        if (firmaId is null)
        {
            return Hata.Yetki("firma_secilmedi", "Bu işlem için bir firma seçili olmalı.").Problem();
        }

        var lisans = await http.RequestServices.GetRequiredService<ILisansSorgusu>()
            .GetirAsync(firmaId.Value, http.RequestAborted);

        if (lisans is null || !lisans.Moduller.Contains(modulKodu))
        {
            return Hata.Yetki("modul_lisansi_yok", "Firmanızın lisansı bu modülü kapsamıyor.").Problem();
        }

        // Süresi dolan lisansta veriler salt okunur kalır: okuma serbest, yazma engellenir.
        var simdi = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        if (lisans.SuresiDolduMu(simdi) && !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
        {
            return LisansHatalari.SuresiDoldu.Problem();
        }

        return await next(context);
    }
}

public static class ModulLisansExtensions
{
    public static TBuilder RequireModul<TBuilder>(this TBuilder builder, string modulKodu)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.AddEndpointFilter(new ModulLisansFiltresi(modulKodu));

        // Lisanstan sonra yetki: modülün her ucu bir yetkiye (ya da açıkça "yetki gerekmez"e) bağlanmalıdır.
        builder.YetkiFiltresiEkle();
        return builder;
    }
}
