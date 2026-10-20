using OnMuhasebe.Modules.Cari.Contracts;

namespace OnMuhasebe.Web.Cari;

public static class CariMetinleri
{
    public static string Tur(CariTuru tur) => tur switch
    {
        CariTuru.Musteri => "Müşteri",
        CariTuru.Tedarikci => "Tedarikçi",
        _ => "Müşteri ve Tedarikçi"
    };

    public static string KisiTipi(KisiTipi tip) => tip == Modules.Cari.Contracts.KisiTipi.Tuzel ? "Tüzel kişi" : "Şahıs";

    public static string HareketTuru(CariHareketTuru tur) => tur switch
    {
        CariHareketTuru.Acilis => "Açılış",
        CariHareketTuru.Dekont => "Dekont",
        CariHareketTuru.SatisFaturasi => "Satış Faturası",
        CariHareketTuru.AlisFaturasi => "Alış Faturası",
        CariHareketTuru.Tahsilat => "Tahsilat",
        CariHareketTuru.Odeme => "Ödeme",
        _ => tur.ToString()
    };

    public static string BakiyeDurumu(BakiyeDurumu durum) => durum switch
    {
        Modules.Cari.Contracts.BakiyeDurumu.Borclu => "Borçlular",
        Modules.Cari.Contracts.BakiyeDurumu.Alacakli => "Alacaklılar",
        Modules.Cari.Contracts.BakiyeDurumu.SifirHaric => "Bakiyesi olanlar",
        _ => "Tümü"
    };
}
