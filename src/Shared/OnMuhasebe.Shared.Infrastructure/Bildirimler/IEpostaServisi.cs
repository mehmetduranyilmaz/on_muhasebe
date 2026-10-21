namespace OnMuhasebe.Shared.Infrastructure.Bildirimler;

// Modüllerin sistem e-postası (ör. şifre sıfırlama) göndermesi; SMTP ayarı ve gönderimi Bildirim modülündedir.
public interface IEpostaServisi
{
    // SMTP tanımlı ya da açık değilse false döner; gönderim hatasında istisna fırlatır.
    Task<bool> GonderAsync(string alici, string aliciAdi, string konu, string html, CancellationToken cancellationToken);
}
