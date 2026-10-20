namespace OnMuhasebe.Shared.Domain.Results;

public sealed record Hata(string Kod, string Mesaj, HataTuru Tur)
{
    public static Hata Dogrulama(string kod, string mesaj) => new(kod, mesaj, HataTuru.Dogrulama);
    public static Hata Bulunamadi(string kod, string mesaj) => new(kod, mesaj, HataTuru.Bulunamadi);
    public static Hata Cakisma(string kod, string mesaj) => new(kod, mesaj, HataTuru.Cakisma);
    public static Hata IsKurali(string kod, string mesaj) => new(kod, mesaj, HataTuru.IsKurali);
    public static Hata Yetki(string kod, string mesaj) => new(kod, mesaj, HataTuru.Yetki);
}
