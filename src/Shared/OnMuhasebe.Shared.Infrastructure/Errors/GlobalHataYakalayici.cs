using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace OnMuhasebe.Shared.Infrastructure.Errors;

internal sealed partial class GlobalHataYakalayici(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalHataYakalayici> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (durum, baslik) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "Kayıt siz düzenlerken başka bir kullanıcı tarafından değiştirildi. Lütfen yenileyip tekrar deneyin."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Bu bilgilerle bir kayıt zaten mevcut."),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu.")
        };

        if (durum == StatusCodes.Status500InternalServerError)
        {
            BeklenmeyenHata(logger, exception);
        }

        httpContext.Response.StatusCode = durum;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = durum, Title = baslik }
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Beklenmeyen hata")]
    private static partial void BeklenmeyenHata(ILogger logger, Exception exception);
}
