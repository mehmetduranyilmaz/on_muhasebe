using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Modules.Bildirim.Domain;

namespace OnMuhasebe.Modules.Bildirim.Infrastructure;

internal sealed record Eposta(string Alici, string AliciAdi, string Konu, string Html);

internal interface IEpostaGonderici
{
    Task GonderAsync(EpostaAyari ayar, IReadOnlyList<Eposta> epostalar, CancellationToken cancellationToken);
}

// SMTP ile gönderim (MailKit). Bir çalışmadaki tüm e-postalar tek bağlantıdan gider.
internal sealed class SmtpEpostaGonderici(SifreKoruyucu sifreKoruyucu) : IEpostaGonderici
{
    public async Task GonderAsync(EpostaAyari ayar, IReadOnlyList<Eposta> epostalar, CancellationToken cancellationToken)
    {
        if (epostalar.Count == 0)
        {
            return;
        }

        var (sunucu, gonderen) = (ayar.Sunucu ?? throw new InvalidOperationException("SMTP sunucusu tanımlı değil."),
            ayar.GonderenAdres ?? throw new InvalidOperationException("Gönderen adresi tanımlı değil."));
        using var istemci = new SmtpClient { Timeout = 20_000 };
        await istemci.ConnectAsync(sunucu, ayar.Port, ayar.Guvenlik switch
        {
            SmtpGuvenligi.Ssl => SecureSocketOptions.SslOnConnect,
            SmtpGuvenligi.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.None
        }, cancellationToken);

        if (ayar.KullaniciAdi is not null)
        {
            await istemci.AuthenticateAsync(ayar.KullaniciAdi, sifreKoruyucu.Coz(ayar.SifreSifreli) ?? "", cancellationToken);
        }

        foreach (var e in epostalar)
        {
            var mesaj = new MimeMessage();
            mesaj.From.Add(new MailboxAddress(ayar.GonderenAd ?? "Ön Muhasebe", gonderen));
            mesaj.To.Add(new MailboxAddress(e.AliciAdi, e.Alici));
            mesaj.Subject = e.Konu;
            mesaj.Body = new BodyBuilder { HtmlBody = e.Html }.ToMessageBody();
            await istemci.SendAsync(mesaj, cancellationToken);
        }

        await istemci.DisconnectAsync(true, cancellationToken);
    }
}
