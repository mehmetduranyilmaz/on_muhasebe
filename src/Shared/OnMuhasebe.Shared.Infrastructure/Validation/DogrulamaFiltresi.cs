using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace OnMuhasebe.Shared.Infrastructure.Validation;

internal sealed class DogrulamaFiltresi<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var istek = context.Arguments.OfType<T>().FirstOrDefault();
        if (istek is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "İstek gövdesi boş.");
        }

        var sonuc = await validator.ValidateAsync(istek, context.HttpContext.RequestAborted);
        return sonuc.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(sonuc.ToDictionary(), title: "Doğrulama hatası.");
    }
}

public static class DogrulamaExtensions
{
    public static RouteHandlerBuilder Dogrula<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<DogrulamaFiltresi<T>>().ProducesValidationProblem();
}
