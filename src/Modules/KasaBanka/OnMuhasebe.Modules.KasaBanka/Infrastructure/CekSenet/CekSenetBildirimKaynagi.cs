using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.KasaBanka.Contracts;
using OnMuhasebe.Shared.Contracts.Bildirimler;
using OnMuhasebe.Shared.Contracts.Moduller;
using OnMuhasebe.Shared.Infrastructure.Bildirimler;

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.CekSenet;

// Çek/senet vadeleri (alınanda tahsil, verilende ödeme zamanı) ve karnesi azalan/dolan çek karneleri.
internal sealed class CekSenetBildirimKaynagi(KasaBankaDbContext db, CekSenetServisi servis, ICariSorgusu cariSorgusu) : IBildirimKaynagi
{
    // İlk çalışmada eski evraklar için bildirim yağmuru olmasın: vadesi en çok bu kadar gün önce geçmiş evrak hatırlatılır.
    public const int GecikmeGunu = 30;

    private static readonly CultureInfo _tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly EvrakDurumu[] _portfoy = [EvrakDurumu.Portfoyde, EvrakDurumu.Tahsilde, EvrakDurumu.Teminatta];

    public string? ModulKodu => ModulKodlari.KasaBanka;

    public async Task<IReadOnlyList<BildirimAdayi>> AdaylarAsync(BildirimBaglami baglam, CancellationToken cancellationToken)
    {
        var adaylar = new List<BildirimAdayi>();
        adaylar.AddRange(await VadeAdaylariAsync(baglam, cancellationToken));
        adaylar.AddRange(await KarneAdaylariAsync(cancellationToken));
        return adaylar;
    }

    private async Task<List<BildirimAdayi>> VadeAdaylariAsync(BildirimBaglami b, CancellationToken ct)
    {
        var (bas, son) = (b.Bugun.AddDays(-GecikmeGunu), b.Bugun.AddDays(b.VadeOncesiGun));
        var evraklar = await db.Evraklar.AsNoTracking()
            .Where(e => (e.Yon == EvrakYonu.Alinan && _portfoy.Contains(e.Durum)) || (e.Yon == EvrakYonu.Verilen && e.Durum == EvrakDurumu.Verildi))
            .Where(e => e.VadeTarihi >= bas && e.VadeTarihi <= son)
            .Select(e => new { e.Id, e.Kod, e.Tur, e.Yon, e.Durum, e.SeriNo, e.VadeTarihi, e.Tutar, e.ParaBirimi, e.BorcluAdi, e.CariHesapId })
            .ToListAsync(ct);
        var unvanlar = evraklar.Count == 0
            ? new Dictionary<Guid, string>()
            : await cariSorgusu.UnvanlarAsync(evraklar.Select(e => e.CariHesapId).Distinct().ToList(), ct);

        var adaylar = new List<BildirimAdayi>();
        foreach (var e in evraklar)
        {
            var alinan = e.Yon == EvrakYonu.Alinan;
            var tur = e.Tur == EvrakTuru.Cek ? "Çek" : "Senet";
            var kalan = e.VadeTarihi.DayNumber - b.Bugun.DayNumber;
            var metin = string.Join(" · ", new[]
            {
                e.Kod, e.SeriNo, alinan ? e.BorcluAdi ?? unvanlar.GetValueOrDefault(e.CariHesapId) : unvanlar.GetValueOrDefault(e.CariHesapId),
                $"{e.Tutar.ToString("N2", _tr)} {e.ParaBirimi}", $"vade {e.VadeTarihi.ToString("dd.MM.yyyy", _tr)}"
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var vade = e.VadeTarihi.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var baglanti = $"cek-senet/{e.Id}";

            if (kalan < 0)
            {
                // Tahsile verilen evrakı banka izler; vadesi geçmiş ama elde duran ve ödenmemiş görünenler hatırlatılır.
                if (e.Durum is EvrakDurumu.Portfoyde or EvrakDurumu.Verildi)
                {
                    adaylar.Add(new BildirimAdayi(BildirimTuru.GecikmisEvrak, $"evrak-gecikti:{e.Id}:{vade}",
                        alinan ? $"Vadesi geçen {tur.ToLower(_tr)} portföyde: {-kalan} gün" : $"Vadesi geçen verilen {tur.ToLower(_tr)} ödendi olarak işaretlenmedi",
                        metin, baglanti, BildirimOnemi.Kritik));
                }
            }
            else
            {
                var bildirimTuru = alinan ? BildirimTuru.AlinanEvrakVadesi : BildirimTuru.VerilenEvrakVadesi;
                var baslik = (alinan, kalan) switch
                {
                    (true, 0) => $"{tur} bugün vadeli: tahsil zamanı",
                    (true, _) => $"{tur} vadesine {kalan} gün kaldı",
                    (false, 0) => $"Verilen {tur.ToLower(_tr)} bugün ödenecek",
                    (false, _) => $"Verilen {tur.ToLower(_tr)} ödemesine {kalan} gün kaldı"
                };
                adaylar.Add(new BildirimAdayi(bildirimTuru, $"{(kalan == 0 ? "evrak-bugun" : "evrak-yaklasan")}:{e.Id}:{vade}", baslik, metin, baglanti,
                    kalan == 0 ? BildirimOnemi.Uyari : BildirimOnemi.Bilgi));
            }
        }

        return adaylar;
    }

    private async Task<List<BildirimAdayi>> KarneAdaylariAsync(CancellationToken ct)
    {
        var esik = await db.CekSenetAyarlari.AsNoTracking().Select(a => (int?)a.KarneUyariEsigi).FirstOrDefaultAsync(ct) ?? 5;
        var karneler = await db.Karneler.AsNoTracking().Include(k => k.Iptaller).Where(k => k.AktifMi).ToListAsync(ct);
        var adaylar = new List<BildirimAdayi>();
        foreach (var k in karneler)
        {
            var kalan = k.Kalan(await servis.KullanilanSerilerAsync(k.Id, ct));
            var ad = k.Tur == EvrakTuru.Cek ? "Çek karnesi" : "Senet defteri";
            var aralik = $"{k.Seri(k.BaslangicNo)} – {k.Seri(k.BitisNo)}";
            if (kalan == 0)
            {
                adaylar.Add(new BildirimAdayi(BildirimTuru.KarneAzaldi, $"karne-doldu:{k.Id}", $"{ad} doldu", $"{aralik}: boş yaprak kalmadı.",
                    "cek-senet/karneler", BildirimOnemi.Uyari));
            }
            else if (esik > 0 && kalan <= esik)
            {
                adaylar.Add(new BildirimAdayi(BildirimTuru.KarneAzaldi, $"karne-azaldi:{k.Id}", $"{ad} azaldı: {kalan} yaprak",
                    $"{aralik}: yeni karne almayı unutmayın.", "cek-senet/karneler", BildirimOnemi.Bilgi));
            }
        }

        return adaylar;
    }
}
