using Microsoft.EntityFrameworkCore;
using Npgsql;
using OnMuhasebe.Modules.Cari.Contracts;
using OnMuhasebe.Modules.Cari.Domain;
using OnMuhasebe.Shared.Domain.Results;
using OnMuhasebe.Shared.Infrastructure.Persistence;

namespace OnMuhasebe.Modules.Cari.Infrastructure;

// Açık kalem takibi: borç ve alacak kalemlerini eşleştirir (FIFO, öncelikli ya da elle).
// Her değişiklik cari satırı kilitlenerek transaction içinde yapılır; eşzamanlı iki tahsilat aynı faturayı iki kez kapatamaz.
internal sealed class CariKapamaServisi(CariDbContext db, IIslemKapsami islemKapsami)
{
    private sealed record Kalem(
        Guid Id, Guid CariHesapId, DateOnly Tarih, DateOnly? Vade, CariHareketTuru Tur, string? BelgeNo, string? Aciklama,
        decimal Borc, decimal Alacak, bool ElleMi, Guid? KaynakBelgeId, decimal Kapanan, bool AvansMi)
    {
        public bool BorcMu => Borc > 0;
        public decimal Tutar => Borc + Alacak;
        public decimal Kalan => Tutar - Kapanan;
        public KapamaKalemi KapamaKalemi() => new(Id, BorcMu, Vade ?? Tarih, Tarih, Kalan);
    }

    // Yeni kalemleri eşleştirir. Öncelik verilirse kalem önce o belgenin kalemlerini kapatır (ör. peşin tahsilat kendi faturasını).
    public Task KapatAsync(Guid cariId, Guid? oncelikliHareketId, Guid? hedefBelgeId, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<bool>(async ct =>
        {
            await KilitleAsync(cariId, ct);
            await EslestirAsync(cariId, oncelikliHareketId, hedefBelgeId, ct);
            return true;
        }, cancellationToken);

    // İptal edilen hareketlerin kapamaları silinir; karşı kalemler yeniden açılır ve otomatik eşleştirilir.
    public Task CozVeKapatAsync(Guid cariId, IReadOnlyCollection<Guid> hareketIdleri, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<bool>(async ct =>
        {
            await KilitleAsync(cariId, ct);
            var kapamalar = await db.CariKapamalari
                .Where(k => hareketIdleri.Contains(k.BorcHareketId) || hareketIdleri.Contains(k.AlacakHareketId))
                .ToListAsync(ct);
            db.CariKapamalari.RemoveRange(kapamalar);
            await db.SaveChangesAsync(ct);
            await EslestirAsync(cariId, null, null, ct);
            return true;
        }, cancellationToken);

    public Task<Sonuc<bool>> ElleKapatAsync(Guid hareketId, IReadOnlyList<ElleKapamaOgesi> ogeler, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<bool>(async ct =>
        {
            var hareket = await db.CariHareketler.SingleOrDefaultAsync(h => h.Id == hareketId, ct);
            if (hareket is null)
            {
                return CariHatalari.HareketBulunamadi;
            }

            if (hareket.IptalEdildiMi || hareket.IptalKaydiMi)
            {
                return CariHatalari.IptalliKalemKapanmaz;
            }

            await KilitleAsync(hareket.CariHesapId, ct);
            var mevcut = await db.CariKapamalari.Where(k => k.BorcHareketId == hareketId || k.AlacakHareketId == hareketId).ToListAsync(ct);
            db.CariKapamalari.RemoveRange(mevcut);
            await db.SaveChangesAsync(ct);

            var kalemler = (await KalemlerAsync(hareket.CariHesapId, ct)).ToDictionary(k => k.Id);
            var borcMu = hareket.Borc > 0;
            if (ogeler.Sum(o => o.Tutar) > hareket.Borc + hareket.Alacak
                || ogeler.Select(o => o.KarsiHareketId).Distinct().Count() != ogeler.Count)
            {
                return CariHatalari.KapamaTutariGecersiz;
            }

            foreach (var oge in ogeler)
            {
                if (!kalemler.TryGetValue(oge.KarsiHareketId, out var karsi) || karsi.BorcMu == borcMu || oge.Tutar <= 0 || oge.Tutar > karsi.Kalan)
                {
                    return CariHatalari.KapamaTutariGecersiz;
                }

                var eslesme = borcMu
                    ? new KapamaEslesmesi(hareketId, karsi.Id, oge.Tutar)
                    : new KapamaEslesmesi(karsi.Id, hareketId, oge.Tutar);
                db.CariKapamalari.Add(CariKapama.Olustur(hareket.CariHesapId, eslesme));
            }

            hareket.ElleKapamaAyarla(true);
            await db.SaveChangesAsync(ct);
            await EslestirAsync(hareket.CariHesapId, null, null, ct);
            return true;
        }, cancellationToken);

    public Task<Sonuc<bool>> OtomatigeDonAsync(Guid hareketId, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<bool>(async ct =>
        {
            var hareket = await db.CariHareketler.SingleOrDefaultAsync(h => h.Id == hareketId, ct);
            if (hareket is null)
            {
                return CariHatalari.HareketBulunamadi;
            }

            await KilitleAsync(hareket.CariHesapId, ct);
            db.CariKapamalari.RemoveRange(
                await db.CariKapamalari.Where(k => k.BorcHareketId == hareketId || k.AlacakHareketId == hareketId).ToListAsync(ct));
            hareket.ElleKapamaAyarla(false);
            await db.SaveChangesAsync(ct);
            await EslestirAsync(hareket.CariHesapId, null, null, ct);
            return true;
        }, cancellationToken);

    // Hedefin (belge kalemleri ya da tek hareket) açık kalanı, aynı carinin ters yöndeki açık avanslarından en eskiden başlanarak kapatılır.
    // Satış faturası alınan avansla, alış faturası verilen avansla mahsup edilir; kapamalar elle kapama gibi kalıcıdır.
    public Task<Sonuc<AvansMahsubuSonucu>> AvansMahsupEtAsync(AvansMahsubuIstegi istek, CancellationToken cancellationToken) =>
        islemKapsami.CalistirAsync<AvansMahsubuSonucu>(async ct =>
        {
            var hedefSorgu = db.CariHareketler.AsNoTracking().Where(h => h.IptalHareketId == null && h.IptalEdilenHareketId == null && !h.AvansMi);
            hedefSorgu = istek.HareketId is { } hid ? hedefSorgu.Where(h => h.Id == hid) : hedefSorgu.Where(h => h.KaynakBelgeId == istek.BelgeId);
            var hedefIdleri = await hedefSorgu.Select(h => new { h.Id, h.CariHesapId }).ToListAsync(ct);
            if (hedefIdleri.Count == 0 || (istek.HareketId is null && istek.BelgeId is null))
            {
                return CariHatalari.MahsupHedefiYok;
            }

            var cariId = hedefIdleri[0].CariHesapId;
            await KilitleAsync(cariId, ct);
            var kalemler = await KalemlerAsync(cariId, ct);
            var idler = hedefIdleri.Select(h => h.Id).ToHashSet();
            var hedefler = kalemler.Where(k => idler.Contains(k.Id) && k.Kalan > 0).OrderBy(k => k.Vade ?? k.Tarih).ToList();
            if (hedefler.Count == 0)
            {
                return CariHatalari.MahsupHedefiYok;
            }

            var borcMu = hedefler[0].BorcMu;
            var avanslar = kalemler.Where(k => k.AvansMi && k.BorcMu != borcMu && k.Kalan > 0).OrderBy(k => k.Tarih).ToList();
            if (avanslar.Count == 0)
            {
                return CariHatalari.AcikAvansYok;
            }

            var kalan = Math.Min(istek.Tutar ?? decimal.MaxValue, hedefler.Sum(h => h.Kalan));
            var mahsup = 0m;
            var avansKalan = avanslar.ToDictionary(a => a.Id, a => a.Kalan);
            foreach (var hedef in hedefler)
            {
                var hedefKalan = hedef.Kalan;
                foreach (var avans in avanslar)
                {
                    var tutar = Math.Min(Math.Min(hedefKalan, avansKalan[avans.Id]), kalan);
                    if (tutar <= 0)
                    {
                        continue;
                    }

                    db.CariKapamalari.Add(CariKapama.Olustur(cariId,
                        borcMu ? new KapamaEslesmesi(hedef.Id, avans.Id, tutar) : new KapamaEslesmesi(avans.Id, hedef.Id, tutar)));
                    (hedefKalan, avansKalan[avans.Id], kalan, mahsup) = (hedefKalan - tutar, avansKalan[avans.Id] - tutar, kalan - tutar, mahsup + tutar);
                }
            }

            await db.SaveChangesAsync(ct);
            return new AvansMahsubuSonucu(mahsup, avansKalan.Values.Sum());
        }, cancellationToken);

    public async Task<CariAvansDurumu?> AvansDurumuAsync(Guid cariId, CancellationToken cancellationToken)
    {
        var paraBirimi = await db.CariHesaplar.AsNoTracking().Where(c => c.Id == cariId).Select(c => c.ParaBirimi).SingleOrDefaultAsync(cancellationToken);
        if (paraBirimi is null)
        {
            return null;
        }

        var avanslar = (await KalemlerAsync(cariId, cancellationToken)).Where(k => k.AvansMi && k.Kalan > 0).ToList();
        return new CariAvansDurumu(paraBirimi, avanslar.Where(a => !a.BorcMu).Sum(a => a.Kalan), avanslar.Where(a => a.BorcMu).Sum(a => a.Kalan));
    }

    public async Task<List<AcikKalem>> AcikKalemlerAsync(Guid? cariId, DateOnly tarih, CancellationToken cancellationToken) =>
        (await KalemlerAsync(cariId, cancellationToken))
            .Where(k => k.Kalan > 0)
            .OrderBy(k => k.Vade ?? k.Tarih).ThenBy(k => k.Tarih)
            .Select(k => AcikKalem(k, tarih))
            .ToList();

    // Firma geneli açık kalemler, carisiyle birlikte (yaşlandırma ve nakit akışı için).
    public async Task<List<(Guid CariHesapId, AcikKalem Kalem)>> FirmaAcikKalemleriAsync(DateOnly tarih, CancellationToken cancellationToken) =>
        (await KalemlerAsync(null, cancellationToken))
            .Where(k => k.Kalan > 0)
            .OrderBy(k => k.Vade ?? k.Tarih).ThenBy(k => k.Tarih)
            .Select(k => (k.CariHesapId, AcikKalem(k, tarih)))
            .ToList();

    public async Task<KapamaDetayi?> DetayAsync(Guid hareketId, CancellationToken cancellationToken)
    {
        var hareket = await db.CariHareketler.AsNoTracking().SingleOrDefaultAsync(h => h.Id == hareketId, cancellationToken);
        if (hareket is null)
        {
            return null;
        }

        var kapamalar = await (
                from k in db.CariKapamalari.AsNoTracking()
                where k.BorcHareketId == hareketId || k.AlacakHareketId == hareketId
                join h in db.CariHareketler on (k.BorcHareketId == hareketId ? k.AlacakHareketId : k.BorcHareketId) equals h.Id
                orderby h.Tarih
                select new KapamaSatiri(h.Id, h.Tarih, h.Tur, h.BelgeNo, k.Tutar, h.KaynakBelgeId))
            .ToListAsync(cancellationToken);

        var tutar = hareket.Borc + hareket.Alacak;
        var kalan = hareket.IptalEdildiMi || hareket.IptalKaydiMi ? 0m : tutar - kapamalar.Sum(k => k.Tutar);
        return new KapamaDetayi(hareket.Id, hareket.Borc > 0 ? HareketYonu.Borc : HareketYonu.Alacak, tutar, kalan, hareket.ElleKapamaMi, kapamalar);
    }

    // Belgenin (fatura vb.) kendi cari kalemlerinden kapanmamış tutar. İptal edilmiş belgeler listede yer almaz.
    public async Task<Dictionary<Guid, decimal>> BelgeKalanlariAsync(IReadOnlyCollection<Guid> belgeIdleri, CancellationToken cancellationToken) =>
        (await db.CariHareketler.AsNoTracking()
            .Where(h => h.KaynakBelgeId != null && belgeIdleri.Contains(h.KaynakBelgeId.Value)
                        && h.IptalHareketId == null && h.IptalEdilenHareketId == null)
            .Select(h => new
            {
                BelgeId = h.KaynakBelgeId!.Value,
                Kalan = h.Borc + h.Alacak
                        - (db.CariKapamalari.Where(k => k.BorcHareketId == h.Id || k.AlacakHareketId == h.Id).Sum(k => (decimal?)k.Tutar) ?? 0m)
            })
            .ToListAsync(cancellationToken))
        .GroupBy(x => x.BelgeId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.Kalan));

    private async Task EslestirAsync(Guid cariId, Guid? oncelikliHareketId, Guid? hedefBelgeId, CancellationToken cancellationToken)
    {
        var kalemler = await KalemlerAsync(cariId, cancellationToken);
        var acik = kalemler.Select(k => k.KapamaKalemi()).ToList();
        var yeni = new List<KapamaEslesmesi>();

        if (oncelikliHareketId is { } oncelikliId && hedefBelgeId is { } belgeId && acik.Find(k => k.Id == oncelikliId) is { } oncelikli)
        {
            var hedefler = kalemler.Where(k => k.KaynakBelgeId == belgeId).Select(k => k.Id).ToHashSet();
            yeni.AddRange(CariKapamaHesaplayici.Oncelikli(oncelikli, acik.Where(k => hedefler.Contains(k.Id))));
            acik = CariKapamaHesaplayici.Dus(acik, yeni);
        }

        var elleKalemler = kalemler.Where(k => k.ElleMi).Select(k => k.Id).ToHashSet();
        yeni.AddRange(CariKapamaHesaplayici.Fifo(acik.Where(k => !elleKalemler.Contains(k.Id))));
        if (yeni.Count == 0)
        {
            return;
        }

        db.CariKapamalari.AddRange(yeni.Select(e => CariKapama.Olustur(cariId, e)));
        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<List<Kalem>> KalemlerAsync(Guid? cariId, CancellationToken cancellationToken)
    {
        var sorgu = db.CariHareketler.AsNoTracking().Where(h => h.IptalHareketId == null && h.IptalEdilenHareketId == null);
        if (cariId is { } id)
        {
            sorgu = sorgu.Where(h => h.CariHesapId == id);
        }

        return sorgu
            .Select(h => new Kalem(h.Id, h.CariHesapId, h.Tarih, h.VadeTarihi, h.Tur, h.BelgeNo, h.Aciklama, h.Borc, h.Alacak, h.ElleKapamaMi,
                h.KaynakBelgeId,
                db.CariKapamalari.Where(k => k.BorcHareketId == h.Id || k.AlacakHareketId == h.Id).Sum(k => (decimal?)k.Tutar) ?? 0m,
                h.AvansMi))
            .ToListAsync(cancellationToken);
    }

    // Satır kilidi transaction sonuna kadar tutulur; aynı carinin kapamaları sırayla hesaplanır.
    private async Task KilitleAsync(Guid cariId, CancellationToken cancellationToken) =>
        await db.Database
            .SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM cari.cari_hesap WHERE id = @id FOR UPDATE", new NpgsqlParameter("id", cariId))
            .ToListAsync(cancellationToken);

    private static AcikKalem AcikKalem(Kalem k, DateOnly tarih) => new(
        k.Id, k.Tarih, k.Vade, k.Tur, k.BorcMu ? HareketYonu.Borc : HareketYonu.Alacak, k.BelgeNo, k.Aciklama,
        k.Tutar, k.Kapanan, k.Kalan, tarih.DayNumber - (k.Vade ?? k.Tarih).DayNumber, k.ElleMi, k.KaynakBelgeId, k.AvansMi);
}
