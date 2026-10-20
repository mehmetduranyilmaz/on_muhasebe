using Microsoft.AspNetCore.Http;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Shared.Infrastructure.Results;

public static class SonucExtensions
{
    public static IResult HttpSonucu(this Sonuc sonuc) =>
        sonuc.BasariliMi ? TypedResults.NoContent() : sonuc.Hata.Problem();

    public static IResult HttpSonucu<T>(this Sonuc<T> sonuc, Func<T, IResult>? basarili = null) =>
        sonuc.BasariliMi
            ? basarili?.Invoke(sonuc.Deger) ?? TypedResults.Ok(sonuc.Deger)
            : sonuc.Hata.Problem();

    public static IResult Problem(this Hata hata) =>
        TypedResults.Problem(
            statusCode: hata.Tur switch
            {
                HataTuru.Dogrulama => StatusCodes.Status400BadRequest,
                HataTuru.Bulunamadi => StatusCodes.Status404NotFound,
                HataTuru.Cakisma => StatusCodes.Status409Conflict,
                HataTuru.IsKurali => StatusCodes.Status422UnprocessableEntity,
                HataTuru.Yetki => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError
            },
            title: hata.Mesaj,
            extensions: new Dictionary<string, object?> { ["kod"] = hata.Kod });
}
