namespace OnMuhasebe.Modules.Yonetim.Domain;

internal static class AbonelikHesabi
{
    // Lisans bitişi, geçerli olduğu son günün ertesi gece yarısıdır (UTC). Süre dolmadıysa uzatma mevcut bitişe eklenir (kalan günler yanmaz);
    // dolduysa bugünden başlar: 21.10'da 1 aylık ödeme 20.11 dahil geçerlidir.
    public static DateTime YeniBitis(DateTime? mevcutBitis, DateTime simdi, int aySayisi)
    {
        var baslangic = mevcutBitis is { } bitis && bitis > simdi ? bitis : simdi.Date;
        return DateTime.SpecifyKind(baslangic, DateTimeKind.Utc).AddMonths(aySayisi);
    }

    public static int KalanGun(DateTime bitis, DateTime simdi) => Math.Max(0, (int)Math.Ceiling((bitis - simdi).TotalDays));
}
