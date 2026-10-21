using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnMuhasebe.Modules.Bildirim.Contracts;
using OnMuhasebe.Modules.Bildirim.Domain;
using OnMuhasebe.Modules.Yonetim.Contracts;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Domain;

namespace OnMuhasebe.Modules.Bildirim.Infrastructure;

// E-postası bekleyen bildirimleri kullanıcı tercihine göre gönderir: anında olanlar her çalışmada, günlük özet sabah bir kez.
// SMTP ayarı yoksa ya da kapalıysa bildirimler beklemede kalır (ayar açılınca gider).
internal sealed partial class EpostaPostacisi(
    BildirimDbContext db,
    IEpostaGonderici gonderici,
    IConfiguration configuration,
    TimeProvider zaman,
    ILogger<EpostaPostacisi> logger)
{
    public async Task<int> GonderAsync(
        IReadOnlyList<FirmaAlicisi> kullanicilar, Dictionary<Guid, KullaniciTercihi> tercihler, CancellationToken cancellationToken)
    {
        var ayar = await db.EpostaAyarlari.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (ayar is not { AktifMi: true, Sunucu: not null, GonderenAdres: not null })
        {
            return 0;
        }

        var bekleyenler = await db.Bildirimler
            .Where(b => b.Eposta == EpostaDurumu.Bekliyor)
            .OrderBy(b => b.OlusturmaTarihi)
            .ToListAsync(cancellationToken);
        if (bekleyenler.Count == 0)
        {
            return 0;
        }

        var simdi = TurkiyeSaati.Simdi(zaman);
        var adres = configuration["Bildirim:UygulamaAdresi"] ?? "http://localhost:5167/";
        var epostalar = new List<Eposta>();
        var gidecek = new List<Domain.Bildirim>();
        foreach (var grup in bekleyenler.GroupBy(b => b.KullaniciId))
        {
            var kullanici = kullanicilar.FirstOrDefault(k => k.KullaniciId == grup.Key);
            var tercihKaydi = tercihler.GetValueOrDefault(grup.Key);
            var tercih = tercihKaydi?.Sozlesme() ?? (kullanici is null ? null : AliciKurallari.Varsayilan(kullanici.Rol));
            if (kullanici is null || tercih is null || tercih.Eposta == EpostaSikligi.Kapali)
            {
                foreach (var b in grup)
                {
                    b.EpostaSonucu(false);
                }

                continue;
            }

            var ozet = tercih.Eposta == EpostaSikligi.GunlukOzet;
            if (ozet && !AliciKurallari.OzetZamaniMi(simdi, tercihKaydi?.SonOzetTarihi))
            {
                continue;
            }

            if (ozet)
            {
                if (tercihKaydi is null)
                {
                    tercihKaydi = KullaniciTercihi.Olustur(grup.Key, tercih);
                    db.Tercihler.Add(tercihKaydi);
                    tercihler[grup.Key] = tercihKaydi;
                }

                tercihKaydi.OzetGonderildi(DateOnly.FromDateTime(simdi));
            }

            var liste = grup.ToList();
            var konu = ozet ? $"Günlük özet: {liste.Count} bildirim" : liste.Count == 1 ? liste[0].Baslik : $"{liste.Count} yeni bildirim";
            epostalar.Add(new Eposta(kullanici.Eposta, kullanici.AdSoyad, konu, Html(kullanici.AdSoyad, liste, adres)));
            gidecek.AddRange(liste);
        }

        var basarili = true;
        try
        {
            await gonderici.GonderAsync(ayar, epostalar, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            basarili = false;
            Gonderilemedi(logger, e);
        }

        foreach (var b in gidecek)
        {
            b.EpostaSonucu(basarili);
        }

        await db.SaveChangesAsync(cancellationToken);
        return basarili ? epostalar.Count : 0;
    }

    internal static string Html(string adSoyad, IReadOnlyList<Domain.Bildirim> bildirimler, string uygulamaAdresi)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:600px;color:#1e293b\">");
        sb.Append($"<h2 style=\"color:#1E3A5F\">Merhaba {WebUtility.HtmlEncode(adSoyad)},</h2>");
        foreach (var b in bildirimler)
        {
            var renk = b.Onem switch { BildirimOnemi.Kritik => "#DC2626", BildirimOnemi.Uyari => "#F59E0B", _ => "#14B8A6" };
            sb.Append($"<div style=\"border-left:4px solid {renk};padding:8px 12px;margin:10px 0;background:#f8fafc\">");
            sb.Append($"<b>{WebUtility.HtmlEncode(b.Baslik)}</b><br/>{WebUtility.HtmlEncode(b.Metin)}");
            if (b.Baglanti is not null)
            {
                sb.Append($"<br/><a href=\"{WebUtility.HtmlEncode(uygulamaAdresi.TrimEnd('/') + "/" + b.Baglanti)}\">Aç</a>");
            }

            sb.Append("</div>");
        }

        sb.Append("<p style=\"color:#64748b;font-size:12px\">Bildirim tercihlerinizi uygulamada profil menüsünden değiştirebilirsiniz.</p></div>");
        return sb.ToString();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bildirim e-postaları gönderilemedi")]
    private static partial void Gonderilemedi(ILogger logger, Exception exception);
}
