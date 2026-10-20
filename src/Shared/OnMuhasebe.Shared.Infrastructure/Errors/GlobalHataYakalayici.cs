using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using OnMuhasebe.Shared.Domain.Abstractions;

namespace OnMuhasebe.Shared.Infrastructure.Errors;

internal sealed partial class GlobalHataYakalayici(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalHataYakalayici> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (durum, baslik, kod) = exception switch
        {
            // Okunamayan istek gövdesi (bozuk JSON, yanlış kodlama) istemci hatasıdır, sunucu hatası değil.
            BadHttpRequestException hatali => (hatali.StatusCode, "İstek okunamadı. Gönderilen bilgileri kontrol edin.", null),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "Kayıt siz düzenlerken başka bir kullanıcı tarafından değiştirildi. Lütfen yenileyip tekrar deneyin.", null),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Bu bilgilerle bir kayıt zaten mevcut.", null),
            DonemKilitliException kilitli => (StatusCodes.Status422UnprocessableEntity, kilitli.Message, "donem_kilitli"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu.", (string?)null)
        };

        if (durum == StatusCodes.Status500InternalServerError)
        {
            BeklenmeyenHata(logger, exception);
        }

        var problem = new ProblemDetails { Status = durum, Title = baslik };
        if (kod is not null)
        {
            problem.Extensions["kod"] = kod;
        }

        httpContext.Response.StatusCode = durum;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Beklenmeyen hata")]
    private static partial void BeklenmeyenHata(ILogger logger, Exception exception);
}
