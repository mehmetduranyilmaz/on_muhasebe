using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

// İşlemin ihtiyaç duyduğu hesap: tahsil/teminat bankası, tahsil/ödeme kasası ya da evrakta kayıtlı hesap.
internal enum HesapIhtiyaci
{
    Yok,
    Banka,
    KasaVeyaBanka,
    // Tahsilde evrakın verildiği banka; verilen çekte bağlı banka hesabı (senette kullanıcı kasa/banka seçer).
    Kayitli
}

// Cari etkisi: hangi cariye, hangi yönde ve hangi hareket türüyle yazılır.
internal enum CariEtkisi
{
    Yok,
    // Alınan evrak girişi: müşteri alacaklanır.
    MusteriAlacak,
    // Portföyden iade: müşteri borçlanır.
    MusteriIade,
    // Karşılıksız: müşteri yeniden borçlanır.
    MusteriKarsiliksiz,
    // Karşılıksızdan tahsil: müşteri alacaklanır (gerçek tahsilat).
    MusteriTahsilat,
    // Ciro: ciro edilen cari borçlanır.
    CiroBorc,
    // Cirodan geri alma: ciro edilen carinin borcu geri alınır.
    CiroIade,
    // Cirodan karşılıksız: ciro carisinin borcu geri alınır ve müşteri yeniden borçlanır.
    CiroKarsiliksiz,
    // Verilen evrak: tedarikçi borçlanır.
    TedarikciBorc,
    // Verilen evrak iade alındı: tedarikçinin borcu geri alınır.
    TedarikciIade,
    // Verilen evrak karşılıksız: tedarikçinin alacağı yeniden doğar.
    TedarikciKarsiliksiz
}

internal sealed record Gecis(
    EvrakIslemi Islem,
    EvrakYonu Yon,
    IReadOnlyList<EvrakDurumu> Kaynaklar,
    EvrakDurumu Hedef,
    HesapIhtiyaci Hesap,
    CariEtkisi Cari,
    // Hesaba giriş (true) ya da çıkış (false); para hareketi yoksa null.
    bool? ParaGirisi = null,
    bool CariGerekli = false);

// Çek/senet yaşam döngüsü: hangi durumdaki evraka hangi işlem uygulanabilir ve sonucu ne olur.
internal static class EvrakGecisleri
{
    private static readonly EvrakDurumu[] _karsiliksizlar = [EvrakDurumu.Karsiliksiz, EvrakDurumu.HukukiTakipte];

    public static IReadOnlyList<Gecis> Tumu { get; } =
    [
        new(EvrakIslemi.TahsileVerme, EvrakYonu.Alinan, [EvrakDurumu.Portfoyde], EvrakDurumu.Tahsilde, HesapIhtiyaci.Banka, CariEtkisi.Yok),
        new(EvrakIslemi.TahsildenGeriAlma, EvrakYonu.Alinan, [EvrakDurumu.Tahsilde], EvrakDurumu.Portfoyde, HesapIhtiyaci.Yok, CariEtkisi.Yok),
        new(EvrakIslemi.Tahsil, EvrakYonu.Alinan, [EvrakDurumu.Tahsilde], EvrakDurumu.TahsilEdildi, HesapIhtiyaci.Kayitli, CariEtkisi.Yok, ParaGirisi: true),
        new(EvrakIslemi.ElTahsil, EvrakYonu.Alinan, [EvrakDurumu.Portfoyde], EvrakDurumu.TahsilEdildi, HesapIhtiyaci.KasaVeyaBanka, CariEtkisi.Yok, ParaGirisi: true),
        new(EvrakIslemi.Ciro, EvrakYonu.Alinan, [EvrakDurumu.Portfoyde], EvrakDurumu.CiroEdildi, HesapIhtiyaci.Yok, CariEtkisi.CiroBorc, CariGerekli: true),
        new(EvrakIslemi.CirodanTahsil, EvrakYonu.Alinan, [EvrakDurumu.CiroEdildi], EvrakDurumu.CirodanKapandi, HesapIhtiyaci.Yok, CariEtkisi.Yok),
        new(EvrakIslemi.CirodanGeriAlma, EvrakYonu.Alinan, [EvrakDurumu.CiroEdildi], EvrakDurumu.Portfoyde, HesapIhtiyaci.Yok, CariEtkisi.CiroIade),
        new(EvrakIslemi.CirodanKarsiliksiz, EvrakYonu.Alinan, [EvrakDurumu.CiroEdildi], EvrakDurumu.Karsiliksiz, HesapIhtiyaci.Yok, CariEtkisi.CiroKarsiliksiz),
        new(EvrakIslemi.Karsiliksiz, EvrakYonu.Alinan, [EvrakDurumu.Portfoyde, EvrakDurumu.Tahsilde], EvrakDurumu.Karsiliksiz, HesapIhtiyaci.Yok, CariEtkisi.MusteriKarsiliksiz),
        new(EvrakIslemi.HukukiTakip, EvrakYonu.Alinan, [EvrakDurumu.Karsiliksiz], EvrakDurumu.HukukiTakipte, HesapIhtiyaci.Yok, CariEtkisi.Yok),
        new(EvrakIslemi.KarsiliksizdanTahsil, EvrakYonu.Alinan, _karsiliksizlar, EvrakDurumu.TahsilEdildi, HesapIhtiyaci.KasaVeyaBanka, CariEtkisi.MusteriTahsilat, ParaGirisi: true),
        new(EvrakIslemi.KarsiliksizIade, EvrakYonu.Alinan, _karsiliksizlar, EvrakDurumu.IadeEdildi, HesapIhtiyaci.Yok, CariEtkisi.Yok),
        new(EvrakIslemi.Iade, EvrakYonu.Alinan, [EvrakDurumu.Portfoyde], EvrakDurumu.IadeEdildi, HesapIhtiyaci.Yok, CariEtkisi.MusteriIade),
        new(EvrakIslemi.TeminataVerme, EvrakYonu.Alinan, [EvrakDurumu.Portfoyde], EvrakDurumu.Teminatta, HesapIhtiyaci.Banka, CariEtkisi.Yok),
        new(EvrakIslemi.TeminattanAlma, EvrakYonu.Alinan, [EvrakDurumu.Teminatta], EvrakDurumu.Portfoyde, HesapIhtiyaci.Yok, CariEtkisi.Yok),
        new(EvrakIslemi.Odeme, EvrakYonu.Verilen, [EvrakDurumu.Verildi], EvrakDurumu.Odendi, HesapIhtiyaci.Kayitli, CariEtkisi.Yok, ParaGirisi: false),
        new(EvrakIslemi.VerilenIade, EvrakYonu.Verilen, [EvrakDurumu.Verildi], EvrakDurumu.IadeAlindi, HesapIhtiyaci.Yok, CariEtkisi.TedarikciIade),
        new(EvrakIslemi.VerilenKarsiliksiz, EvrakYonu.Verilen, [EvrakDurumu.Verildi], EvrakDurumu.Karsiliksiz, HesapIhtiyaci.Yok, CariEtkisi.TedarikciKarsiliksiz),
        new(EvrakIslemi.KarsiliksizGeriAlma, EvrakYonu.Verilen, [EvrakDurumu.Karsiliksiz], EvrakDurumu.IadeAlindi, HesapIhtiyaci.Yok, CariEtkisi.Yok)
    ];

    public static Gecis? Bul(EvrakYonu yon, EvrakDurumu durum, EvrakIslemi islem) =>
        Tumu.FirstOrDefault(g => g.Islem == islem && g.Yon == yon && g.Kaynaklar.Contains(durum));

    public static IReadOnlyList<EvrakIslemi> Uygunlar(EvrakYonu yon, EvrakDurumu durum) =>
        Tumu.Where(g => g.Yon == yon && g.Kaynaklar.Contains(durum)).Select(g => g.Islem).ToList();

    // Evrak artık firmanın takibinde değil (kapandı ya da iade edildi).
    public static bool KapaliMi(EvrakDurumu durum) => durum is EvrakDurumu.TahsilEdildi or EvrakDurumu.CirodanKapandi or EvrakDurumu.IadeEdildi
        or EvrakDurumu.Odendi or EvrakDurumu.IadeAlindi or EvrakDurumu.Iptal;

    // Vadesinde firmaya para girecek alınan evrak (ciro edilen artık bizim değil).
    public static bool PortfoyMu(EvrakDurumu durum) => durum is EvrakDurumu.Portfoyde or EvrakDurumu.Tahsilde or EvrakDurumu.Teminatta;
}
