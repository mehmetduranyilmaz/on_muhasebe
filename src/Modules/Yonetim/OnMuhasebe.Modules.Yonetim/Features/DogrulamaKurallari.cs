using FluentValidation;
using OnMuhasebe.Modules.Yonetim.Domain;
using OnMuhasebe.Shared.Contracts.Kimlik;
using OnMuhasebe.Shared.Domain.Dogrulama;

namespace OnMuhasebe.Modules.Yonetim.Features;

internal static class DogrulamaKurallari
{
    public static IRuleBuilderOptions<T, string?> Sifre<T>(this IRuleBuilder<T, string?> kural) =>
        kural
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Za-zÇĞİÖŞÜçğıöşü]").WithMessage("Şifre en az bir harf içermelidir.")
            .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermelidir.");

    public static IRuleBuilderOptions<T, string> Eposta<T>(this IRuleBuilder<T, string> kural) =>
        kural.NotEmpty().EmailAddress().MaximumLength(256);

    public static IRuleBuilderOptions<T, string?> VergiNo<T>(this IRuleBuilder<T, string?> kural) =>
        kural.Must(v => string.IsNullOrWhiteSpace(v) || VergiKimlik.GecerliMi(v.Trim()))
            .WithMessage("Geçerli bir vergi kimlik (10 hane) veya T.C. kimlik (11 hane) numarası girin.");
}
