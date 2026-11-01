namespace OnMuhasebe.Shared.Contracts.Bildirimler;

// Süreli yükümlülük hatırlatma aşamaları: her aşama bir bitiş tarihi için bir kez bildirilir; yenilenince anahtar değişir, döngü baştan başlar.
public static class SureliHatirlatma
{
    public const int SonUyariGunu = 7;

    // Uzun süredir dolmuş kayıt ilk çalışmada bildirim yağmuru yapmasın.
    public const int GecikmeGunu = 30;

    public const string Yaklasiyor = "yaklasiyor";
    public const string Son = "son";
    public const string Doldu = "doldu";

    public static string? Asama(DateOnly bitis, int hatirlatmaGun, DateOnly bugun)
    {
        var kalan = bitis.DayNumber - bugun.DayNumber;
        return kalan switch
        {
            < -GecikmeGunu => null,
            < 0 => Doldu,
            _ when kalan <= SonUyariGunu => Son,
            _ when kalan <= hatirlatmaGun => Yaklasiyor,
            _ => null
        };
    }
}
