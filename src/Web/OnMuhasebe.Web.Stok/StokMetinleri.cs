using OnMuhasebe.Modules.Stok.Contracts;

namespace OnMuhasebe.Web.Stok;

public static class StokMetinleri
{
    public static string Tur(StokTuru tur) => tur == StokTuru.Urun ? "Ürün" : "Hizmet";

    public static string HareketTuru(StokHareketTuru tur) => tur switch
    {
        StokHareketTuru.Acilis => "Açılış",
        StokHareketTuru.SayimFazlasi => "Sayım fazlası",
        StokHareketTuru.SayimEksigi => "Sayım eksiği",
        StokHareketTuru.Fire => "Fire",
        StokHareketTuru.Sarf => "Sarf",
        StokHareketTuru.TransferGiris => "Transfer girişi",
        StokHareketTuru.TransferCikis => "Transfer çıkışı",
        StokHareketTuru.AlisFaturasi => "Alış faturası",
        StokHareketTuru.SatisFaturasi => "Satış faturası",
        _ => tur.ToString()
    };

    // Elle girilebilen türler ve kısa açıklamaları; hareket diyaloğunda kullanılır.
    public static IReadOnlyList<(StokHareketTuru Tur, string Aciklama)> ElleTurler { get; } =
    [
        (StokHareketTuru.Acilis, "Programa geçerken eldeki stok"),
        (StokHareketTuru.SayimFazlasi, "Sayımda kayıttan fazla çıkan"),
        (StokHareketTuru.SayimEksigi, "Sayımda kayıttan eksik çıkan"),
        (StokHareketTuru.Fire, "Bozulan, kırılan, kaybolan"),
        (StokHareketTuru.Sarf, "İşletme içinde kullanılan")
    ];

    public static bool GirisMi(StokHareketTuru tur) =>
        tur is StokHareketTuru.Acilis or StokHareketTuru.SayimFazlasi or StokHareketTuru.TransferGiris or StokHareketTuru.AlisFaturasi;

    public static string MaliyetYontemi(MaliyetYontemi yontem) => yontem switch
    {
        Modules.Stok.Contracts.MaliyetYontemi.Fifo => "FIFO (ilk giren ilk çıkar)",
        Modules.Stok.Contracts.MaliyetYontemi.SonAlisFiyati => "Son alış fiyatı",
        _ => "Ağırlıklı ortalama"
    };

    public static string MaliyetYontemiAciklamasi(MaliyetYontemi yontem) => yontem switch
    {
        Modules.Stok.Contracts.MaliyetYontemi.Fifo => "Çıkışlar en eski alışların fiyatından düşülür. Fiyatlar yükselirken stok değeri güncel fiyatlara yakın kalır.",
        Modules.Stok.Contracts.MaliyetYontemi.SonAlisFiyati => "Tüm stok ve çıkışlar en son giriş fiyatıyla değerlenir. Basittir ama muhasebe açısından en az hassas yöntemdir.",
        _ => "Her girişte ortalama maliyet yeniden hesaplanır, çıkışlar o anki ortalamayla değerlenir. Ön muhasebede en yaygın yöntemdir."
    };

    public static string EksiStok(EksiStokDavranisi davranis) => davranis switch
    {
        EksiStokDavranisi.IzinVer => "İzin ver",
        EksiStokDavranisi.Engelle => "Engelle",
        _ => "İzin ver ama uyar"
    };

    public static string EksiStokAciklamasi(EksiStokDavranisi davranis) => davranis switch
    {
        EksiStokDavranisi.IzinVer => "Stok eksiye düşse de işlem sessizce kaydedilir.",
        EksiStokDavranisi.Engelle => "Depoda yeterli stok yoksa çıkış ve satış kaydedilmez.",
        _ => "İşlem kaydedilir, stok eksiye düşerse ekranda uyarı gösterilir."
    };
}
