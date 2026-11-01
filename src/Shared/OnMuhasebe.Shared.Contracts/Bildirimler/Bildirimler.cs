using System.Text.Json.Serialization;

namespace OnMuhasebe.Shared.Contracts.Bildirimler;

[JsonConverter(typeof(JsonStringEnumConverter<BildirimTuru>))]
public enum BildirimTuru
{
    AlinanEvrakVadesi,
    VerilenEvrakVadesi,
    GecikmisEvrak,
    RiskLimiti,
    KarneAzaldi,
    AbonelikBitiyor,
    OdemeBildirimiSonucu,
    EvrakSuresi,
    AracSuresi,
    BakimSuresi
}

[JsonConverter(typeof(JsonStringEnumConverter<BildirimOnemi>))]
public enum BildirimOnemi
{
    Bilgi,
    Uyari,
    Kritik
}

// Bir modülün ürettiği bildirim. Anahtar aynı olayı tekilleştirir (ör. "evrak-vade:{id}:3gun"); aynı anahtar bir kullanıcıya
// bir kez yazılır. Baglanti, tıklanınca açılacak sayfadır (ör. "cek-senet/{id}").
// HedefKullaniciId: bu kişi (ör. evrağın sorumlusu) tercihinden bağımsız bildirim alır; diğer kullanıcılar tercihlerine göre.
public sealed record BildirimAdayi(BildirimTuru Tur, string Anahtar, string Baslik, string Metin, string? Baglanti, BildirimOnemi Onem, Guid? HedefKullaniciId = null);

public static class BildirimTurleri
{
    public static string Ad(BildirimTuru tur) => tur switch
    {
        BildirimTuru.AlinanEvrakVadesi => "Alınan çek/senet vadesi",
        BildirimTuru.VerilenEvrakVadesi => "Verilen çek/senet ödemesi",
        BildirimTuru.GecikmisEvrak => "Vadesi geçen evrak",
        BildirimTuru.RiskLimiti => "Risk limiti aşımı",
        BildirimTuru.KarneAzaldi => "Çek karnesi / senet defteri azaldı",
        BildirimTuru.AbonelikBitiyor => "Abonelik süresi",
        BildirimTuru.EvrakSuresi => "Evrak süresi",
        BildirimTuru.AracSuresi => "Araç sigorta, muayene ve MTV süresi",
        BildirimTuru.BakimSuresi => "Ekipman bakımı ve yasal kontrol",
        BildirimTuru.OdemeBildirimiSonucu => "Ödeme bildirimi sonucu",
        _ => tur.ToString()
    };
}
