using System.Globalization;
using OnMuhasebe.Modules.KasaBanka.Contracts;

namespace OnMuhasebe.Modules.KasaBanka.Domain.CekSenet;

internal static class RiskMesaji
{
    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");

    public static bool RiskliMi(bool karaListede, bool uyariKapali, int karsiliksizSayisi) => karaListede || (karsiliksizSayisi > 0 && !uyariKapali);

    // "Ahmet Yılmaz (12345678901): 12.03.2026 tarihli 15.000,00 TRY evrakı karşılıksız çıktı (takipte)."
    public static string Olustur(IReadOnlyList<RiskliBorclu> borclular)
    {
        var parcalar = borclular.Select(b =>
        {
            var kim = b.KimlikNo is null ? b.Ad : $"{b.Ad} ({b.KimlikNo})";
            var gecmis = b.Karsiliksizlar
                .OrderByDescending(k => k.KarsiliksizTarihi)
                .Select(k => $"{k.KarsiliksizTarihi.ToString("dd.MM.yyyy", _tr)} tarihli {k.Tutar.ToString("N2", _tr)} {k.ParaBirimi} evrakı karşılıksız çıktı ({DurumMetni(k.GuncelDurum)})")
                .ToList();
            if (b.KaraListede)
            {
                gecmis.Insert(0, b.RiskNotu is null ? "kara listede" : $"kara listede: {b.RiskNotu}");
            }

            return $"{kim}: {string.Join("; ", gecmis)}.";
        });
        return string.Join(" ", parcalar);
    }

    public static string DurumMetni(EvrakDurumu durum) => durum switch
    {
        EvrakDurumu.Karsiliksiz => "takipte",
        EvrakDurumu.HukukiTakipte => "hukuki takipte",
        EvrakDurumu.TahsilEdildi => "sonradan ödendi",
        EvrakDurumu.IadeEdildi => "iade edildi",
        _ => "kapandı"
    };
}

internal static class EvrakAdlari
{
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
}

internal sealed record VadeKalemi(DateOnly Vade, EvrakYonu Yon, decimal TutarTl);

internal static class VadeTakvimiHesaplayici
{
    // Bugünden önceki vadeler "gecikmiş" toplanır; dilimler [bas, son] aralığında hafta (Pazartesi başlangıçlı) ya da ay olarak boş olanlar dahil üretilir.
    public static VadeTakvimi Olustur(IReadOnlyList<VadeKalemi> kalemler, DateOnly bugun, DateOnly bas, DateOnly son, VadeGruplama gruplama)
    {
        var gecikmis = kalemler.Where(k => k.Vade < bugun).ToList();
        var dilimler = new List<VadeDilimi>();
        for (var d = DilimBasi(bas, gruplama); d <= son; d = Sonraki(d, gruplama))
        {
            var (dBas, dSon) = (d, Sonraki(d, gruplama).AddDays(-1));
            var icindekiler = kalemler.Where(k => k.Vade >= dBas && k.Vade <= dSon && k.Vade >= bugun).ToList();
            dilimler.Add(new VadeDilimi(dBas, dSon,
                icindekiler.Where(k => k.Yon == EvrakYonu.Alinan).Sum(k => k.TutarTl),
                icindekiler.Where(k => k.Yon == EvrakYonu.Verilen).Sum(k => k.TutarTl),
                icindekiler.Count(k => k.Yon == EvrakYonu.Alinan),
                icindekiler.Count(k => k.Yon == EvrakYonu.Verilen)));
        }

        return new VadeTakvimi(
            gecikmis.Where(k => k.Yon == EvrakYonu.Alinan).Sum(k => k.TutarTl),
            gecikmis.Where(k => k.Yon == EvrakYonu.Verilen).Sum(k => k.TutarTl),
            gecikmis.Count(k => k.Yon == EvrakYonu.Alinan),
            gecikmis.Count(k => k.Yon == EvrakYonu.Verilen),
            dilimler);
    }

    private static DateOnly DilimBasi(DateOnly tarih, VadeGruplama g) => g == VadeGruplama.Ay
        ? new DateOnly(tarih.Year, tarih.Month, 1)
        : tarih.AddDays(-(((int)tarih.DayOfWeek + 6) % 7));

    private static DateOnly Sonraki(DateOnly d, VadeGruplama g) => g == VadeGruplama.Ay ? d.AddMonths(1) : d.AddDays(7);
}
