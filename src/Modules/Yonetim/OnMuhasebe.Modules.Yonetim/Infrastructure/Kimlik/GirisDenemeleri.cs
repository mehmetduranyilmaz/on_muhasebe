using Microsoft.Extensions.Caching.Memory;

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Kimlik;

// Hatalı şifre denemeleri hesaba değil e-posta + IP çiftine sayılır: başkası kendi IP'sinden deneyerek bir kullanıcıyı
// (sistem yöneticisi dahil) kilitleyemez. Çok IP'den dağıtık denemeye karşı e-posta başına ayrıca geniş bir sınır vardır.
// Kayıtlı olmayan e-postalar da aynı sayılır; kilit yanıtı hesabın varlığını ele vermez. Sayaçlar bellektedir (tek API örneği).
internal sealed class GirisDenemeleri(IMemoryCache onbellek)
{
    public const int IpBasinaAzami = 5;
    public const int EpostaBasinaAzami = 50;
    public static readonly TimeSpan Pencere = TimeSpan.FromMinutes(15);

    private sealed class Sayac
    {
        public int Deger;
    }

    public bool KilitliMi(string eposta, string? ip) =>
        Deger(CiftAnahtari(eposta, ip)) >= IpBasinaAzami || Deger(EpostaAnahtari(eposta)) >= EpostaBasinaAzami;

    public void Basarisiz(string eposta, string? ip)
    {
        Artir(CiftAnahtari(eposta, ip));
        Artir(EpostaAnahtari(eposta));
    }

    public void Basarili(string eposta, string? ip) => onbellek.Remove(CiftAnahtari(eposta, ip));

    // Şifre e-postayla sıfırlanınca (sahiplik kanıtlandı) bu e-postanın bütün IP'lerdeki kilitleri birlikte kalkar.
    public void Sifirla(string eposta) => onbellek.Set(SurumAnahtari(eposta), Surum(eposta) + 1, Pencere);

    private int Surum(string eposta) => onbellek.TryGetValue(SurumAnahtari(eposta), out int surum) ? surum : 0;

    private int Deger(string anahtar) => onbellek.TryGetValue(anahtar, out Sayac? sayac) ? Volatile.Read(ref sayac!.Deger) : 0;

    private void Artir(string anahtar)
    {
        var sayac = onbellek.GetOrCreate(anahtar, girdi =>
        {
            girdi.AbsoluteExpirationRelativeToNow = Pencere;
            return new Sayac();
        })!;
        Interlocked.Increment(ref sayac.Deger);
    }

    private static string SurumAnahtari(string eposta) => $"giris-surum:{eposta}";

    private string EpostaAnahtari(string eposta) => $"giris:{Surum(eposta)}:{eposta}";

    private string CiftAnahtari(string eposta, string? ip) => $"giris:{Surum(eposta)}:{eposta}:{ip}";
}
