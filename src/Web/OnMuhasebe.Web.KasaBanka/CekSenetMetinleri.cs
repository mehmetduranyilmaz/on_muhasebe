using MudBlazor;
using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Web.KasaBanka;

// İşlem diyaloğunun istediği ek bilgi; API'deki geçiş tablosunun hesap/cari ihtiyacıyla aynıdır.
public enum IslemGirdisi
{
    Yok,
    Banka,
    KasaVeyaBanka,
    // Verilen çek bağlı hesabından ödenir; seçimde senet varsa kasa/banka sorulur.
    SenetIcinKasaVeyaBanka,
    Cari
}

public static class CekSenetMetinleri
{
    public static string Tur(EvrakTuru tur) => tur == EvrakTuru.Cek ? "Çek" : "Senet";

    public static string Yon(EvrakYonu yon) => yon == EvrakYonu.Alinan ? "Alınan" : "Verilen";

    public static string TurIkonu(EvrakTuru tur) => tur == EvrakTuru.Cek ? Icons.Material.Filled.RequestQuote : Icons.Material.Filled.Description;

    public static string Durum(EvrakDurumu durum) => durum switch
    {
        EvrakDurumu.Portfoyde => "Portföyde",
        EvrakDurumu.Tahsilde => "Tahsilde",
        EvrakDurumu.Teminatta => "Teminatta",
        EvrakDurumu.CiroEdildi => "Ciro edildi",
        EvrakDurumu.Karsiliksiz => "Karşılıksız",
        EvrakDurumu.HukukiTakipte => "Hukuki takipte",
        EvrakDurumu.TahsilEdildi => "Tahsil edildi",
        EvrakDurumu.CirodanKapandi => "Cirodan kapandı",
        EvrakDurumu.IadeEdildi => "İade edildi",
        EvrakDurumu.Verildi => "Verildi",
        EvrakDurumu.Odendi => "Ödendi",
        EvrakDurumu.IadeAlindi => "İade alındı",
        _ => "İptal"
    };

    public static string DurumRengi(EvrakDurumu durum) => durum switch
    {
        EvrakDurumu.Portfoyde => "#14B8A6",
        EvrakDurumu.Tahsilde => "#3B82F6",
        EvrakDurumu.Teminatta => "#6366F1",
        EvrakDurumu.CiroEdildi => "#8B5CF6",
        EvrakDurumu.Karsiliksiz => "#DC2626",
        EvrakDurumu.HukukiTakipte => "#991B1B",
        EvrakDurumu.TahsilEdildi or EvrakDurumu.CirodanKapandi or EvrakDurumu.Odendi => "#16A34A",
        EvrakDurumu.Verildi => "#F59E0B",
        _ => "#64748B"
    };

    // Vade hâlâ anlamlı mı (karşılıksız ve kapanan evrakta geri sayım gösterilmez).
    public static bool VadeTakipliMi(EvrakDurumu durum) => durum is EvrakDurumu.Portfoyde or EvrakDurumu.Tahsilde or EvrakDurumu.Teminatta
        or EvrakDurumu.CiroEdildi or EvrakDurumu.Verildi;

    public static bool KapaliMi(EvrakDurumu durum) => durum is EvrakDurumu.TahsilEdildi or EvrakDurumu.CirodanKapandi or EvrakDurumu.IadeEdildi
        or EvrakDurumu.Odendi or EvrakDurumu.IadeAlindi or EvrakDurumu.Iptal;

    public static string Islem(EvrakIslemi islem) => islem switch
    {
        EvrakIslemi.Giris => "Portföye giriş",
        EvrakIslemi.Cikis => "Tedarikçiye verildi",
        EvrakIslemi.TahsileVerme => "Tahsile ver",
        EvrakIslemi.TahsildenGeriAlma => "Tahsilden geri al",
        EvrakIslemi.Tahsil => "Tahsil edildi",
        EvrakIslemi.ElTahsil => "Elden tahsil",
        EvrakIslemi.Ciro => "Ciro et",
        EvrakIslemi.CirodanTahsil => "Cirodan kapandı",
        EvrakIslemi.CirodanGeriAlma => "Cirodan geri al",
        EvrakIslemi.CirodanKarsiliksiz => "Cirodan karşılıksız döndü",
        EvrakIslemi.Karsiliksiz => "Karşılıksız / protesto",
        EvrakIslemi.HukukiTakip => "Hukuki takibe ver",
        EvrakIslemi.KarsiliksizdanTahsil => "Karşılıksızdan tahsil",
        EvrakIslemi.KarsiliksizIade => "Karşılıksız evrakı iade et",
        EvrakIslemi.Iade => "Müşteriye iade et",
        EvrakIslemi.TeminataVerme => "Teminata ver",
        EvrakIslemi.TeminattanAlma => "Teminattan al",
        EvrakIslemi.Odeme => "Ödendi",
        EvrakIslemi.VerilenIade => "İade alındı",
        EvrakIslemi.VerilenKarsiliksiz => "Karşılıksız çıktı",
        _ => "Karşılıksız evrakı geri aldık"
    };

    // İşlemin cari ve kasa etkisinin kullanıcıya açıklaması.
    public static string IslemAciklamasi(EvrakIslemi islem) => islem switch
    {
        EvrakIslemi.TahsileVerme => "Evrak tahsil için bankaya verilir; vadesinde \"Tahsil edildi\" ile banka hesabına girer.",
        EvrakIslemi.TahsildenGeriAlma => "Bankadaki evrak tahsil edilmeden geri alınır, portföye döner.",
        EvrakIslemi.Tahsil => "Tutar, evrakın verildiği banka hesabına giriş olarak yazılır.",
        EvrakIslemi.ElTahsil => "Evrak bedeli elden ya da havaleyle alındı; seçilen hesaba giriş yazılır.",
        EvrakIslemi.Ciro => "Evrak tedarikçiye ödeme olarak verilir; seçilen cari borçlandırılır.",
        EvrakIslemi.CirodanTahsil => "Ciro edilen evrak vadesinde ödendi; takibi kapanır. Para hareketi oluşmaz.",
        EvrakIslemi.CirodanGeriAlma => "Ciro edilen cari evrakı geri verdi; carinin borçlanması geri alınır, evrak portföye döner.",
        EvrakIslemi.CirodanKarsiliksiz => "Ciro edilen evrak karşılıksız çıktı ve geri geldi: ciro carisinin borcu geri alınır, müşteri yeniden borçlanır, evrak takibe düşer.",
        EvrakIslemi.Karsiliksiz => "Müşteri evrak tutarı kadar yeniden borçlandırılır. Evrak takip için firmada kalır; keşideci kara listeye girer.",
        EvrakIslemi.HukukiTakip => "Evrak avukata/icraya verildi. Para hareketi oluşmaz.",
        EvrakIslemi.KarsiliksizdanTahsil => "Müşterinin ödediği tutar seçilen hesaba girer ve müşteri alacaklanır. Kısmi ödeme girilebilir; tamamı ödenince evrak kapanır.",
        EvrakIslemi.KarsiliksizIade => "Karşılıksız evrak müşteriye teslim edilir. Müşteri karşılıksız anında borçlandığı için cari etkilenmez.",
        EvrakIslemi.Iade => "Vadesi gelmemiş evrak müşteriye geri verilir; müşteri evrak tutarı kadar borçlandırılır.",
        EvrakIslemi.TeminataVerme => "Evrak kredi teminatı olarak bankaya verilir. Para hareketi oluşmaz.",
        EvrakIslemi.TeminattanAlma => "Teminattaki evrak bankadan geri alınır, portföye döner.",
        EvrakIslemi.Odeme => "Çek bağlı olduğu banka hesabından, senet seçilen kasa/banka hesabından ödenir.",
        EvrakIslemi.VerilenIade => "Tedarikçi evrakı geri verdi; tedarikçinin borçlanması geri alınır.",
        EvrakIslemi.VerilenKarsiliksiz => "Verdiğimiz evrak ödenemedi; tedarikçinin alacağı yeniden doğar.",
        EvrakIslemi.KarsiliksizGeriAlma => "Karşılıksız çıkan evrakımız tedarikçiden geri alındı. Cari etkilenmez.",
        _ => ""
    };

    public static string IslemIkonu(EvrakIslemi islem) => islem switch
    {
        EvrakIslemi.Giris => Icons.Material.Filled.Inventory,
        EvrakIslemi.Cikis => Icons.Material.Filled.Outbox,
        EvrakIslemi.TahsileVerme => Icons.Material.Filled.AccountBalance,
        EvrakIslemi.Tahsil or EvrakIslemi.Odeme or EvrakIslemi.CirodanTahsil => Icons.Material.Filled.TaskAlt,
        EvrakIslemi.ElTahsil or EvrakIslemi.KarsiliksizdanTahsil => Icons.Material.Filled.Payments,
        EvrakIslemi.Ciro => Icons.Material.Filled.Forward,
        EvrakIslemi.Karsiliksiz or EvrakIslemi.CirodanKarsiliksiz or EvrakIslemi.VerilenKarsiliksiz => Icons.Material.Filled.Report,
        EvrakIslemi.HukukiTakip => Icons.Material.Filled.Gavel,
        EvrakIslemi.TeminataVerme => Icons.Material.Filled.Shield,
        EvrakIslemi.Iade or EvrakIslemi.KarsiliksizIade or EvrakIslemi.VerilenIade or EvrakIslemi.KarsiliksizGeriAlma => Icons.Material.Filled.KeyboardReturn,
        _ => Icons.Material.Filled.Undo
    };

    public static string IslemRengi(EvrakIslemi islem) => islem switch
    {
        EvrakIslemi.Tahsil or EvrakIslemi.ElTahsil or EvrakIslemi.KarsiliksizdanTahsil or EvrakIslemi.Odeme or EvrakIslemi.CirodanTahsil => "#16A34A",
        EvrakIslemi.Karsiliksiz or EvrakIslemi.CirodanKarsiliksiz or EvrakIslemi.VerilenKarsiliksiz => "#DC2626",
        EvrakIslemi.HukukiTakip => "#991B1B",
        EvrakIslemi.Ciro => "#8B5CF6",
        EvrakIslemi.TahsileVerme or EvrakIslemi.TeminataVerme or EvrakIslemi.Giris => "#3B82F6",
        EvrakIslemi.Cikis => "#F59E0B",
        EvrakIslemi.Iade or EvrakIslemi.KarsiliksizIade or EvrakIslemi.VerilenIade or EvrakIslemi.KarsiliksizGeriAlma => "#D97706",
        _ => "#64748B"
    };

    // Kalıcı/riskli sonuçları olan işlemler onay ister ve kırmızı düğmeyle gösterilir.
    public static bool TehlikeliMi(EvrakIslemi islem) => islem is EvrakIslemi.Karsiliksiz or EvrakIslemi.CirodanKarsiliksiz or EvrakIslemi.VerilenKarsiliksiz;

    public static IslemGirdisi Girdi(EvrakIslemi islem) => islem switch
    {
        EvrakIslemi.TahsileVerme or EvrakIslemi.TeminataVerme => IslemGirdisi.Banka,
        EvrakIslemi.ElTahsil or EvrakIslemi.KarsiliksizdanTahsil => IslemGirdisi.KasaVeyaBanka,
        EvrakIslemi.Odeme => IslemGirdisi.SenetIcinKasaVeyaBanka,
        EvrakIslemi.Ciro => IslemGirdisi.Cari,
        _ => IslemGirdisi.Yok
    };

    public static string RiskDavranisi(RiskDavranisi d) => d switch
    {
        Modules.KasaBanka.Contracts.RiskDavranisi.UyarVeOnayIste => "Uyar ve onay iste",
        Modules.KasaBanka.Contracts.RiskDavranisi.Engelle => "Kabul etme",
        _ => "Kontrol etme"
    };

    public static string RiskDavranisiAciklamasi(RiskDavranisi d) => d switch
    {
        Modules.KasaBanka.Contracts.RiskDavranisi.UyarVeOnayIste => "Riskli keşideci/borçludan evrak girilirken geçmiş gösterilir, kullanıcı onaylarsa kaydedilir.",
        Modules.KasaBanka.Contracts.RiskDavranisi.Engelle => "Kara listedeki ya da karşılıksız geçmişi olan keşideci/borçludan evrak kabul edilmez.",
        _ => "Kara liste kontrolü yapılmaz."
    };

    public static string Vade(DateOnly vade, DateOnly bugun) => (vade.DayNumber - bugun.DayNumber) switch
    {
        0 => "Bugün",
        1 => "Yarın",
        < 0 and var g => $"{-g} gün geçti",
        var g => $"{g} gün kaldı"
    };
}
