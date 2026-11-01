using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OnMuhasebe.Modules.Muhasebe.Contracts;
using OnMuhasebe.Shared.Domain.Abstractions;
using OnMuhasebe.Shared.Domain.Entities;

namespace OnMuhasebe.Modules.Muhasebe.Domain;

// Dönemin muhasebeye aktarıldı işareti. İptal edilirse fişleri yeniden "aktarılmadı" sayılır.
internal sealed class MuhasebeAktarimi : FirmaEntity, IIsyeriVarligi
{
    private MuhasebeAktarimi() { }

    public DateOnly Baslangic { get; private set; }
    public DateOnly Bitis { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public int FisSayisi { get; private set; }
    public decimal ToplamBorc { get; private set; }
    public string? Aciklama { get; private set; }
    public string? AktaranAdi { get; private set; }
    public DateTime? IptalTarihi { get; private set; }

    public static MuhasebeAktarimi Olustur(
        DateOnly baslangic, DateOnly bitis, Guid? isyeriId, int fisSayisi, decimal toplamBorc, string? aciklama, string? aktaranAdi) => new()
    {
        Baslangic = baslangic,
        Bitis = bitis,
        IsyeriId = isyeriId,
        FisSayisi = fisSayisi,
        ToplamBorc = toplamBorc,
        Aciklama = string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim(),
        AktaranAdi = aktaranAdi
    };

    public void IptalEt(DateTime simdi) => IptalTarihi ??= simdi;
}

// Fişin son aktarıldığı hali: kaynak belge başına tek satır. Yeniden aktarımda eski satırın yerini alır.
internal sealed class AktarilanFis : FirmaEntity, IIsyeriVarligi
{
    private AktarilanFis() { }

    public Guid AktarimId { get; private set; }
    public string Kaynak { get; private set; } = null!;
    public Guid KaynakId { get; private set; }
    public string BelgeTuru { get; private set; } = null!;
    public DateOnly Tarih { get; private set; }
    public string? BelgeNo { get; private set; }
    public Guid? IsyeriId { get; private set; }
    public string Ozet { get; private set; } = null!;

    public static AktarilanFis Olustur(Guid aktarimId, MuhasebeFisi fis) => new()
    {
        AktarimId = aktarimId,
        Kaynak = fis.Kaynak,
        KaynakId = fis.KaynakId,
        BelgeTuru = fis.BelgeTuru,
        Tarih = fis.Tarih,
        BelgeNo = fis.BelgeNo,
        IsyeriId = fis.IsyeriId,
        Ozet = FisOzeti.Hesapla(fis)
    };
}

// Değişiklik takibi için içerik özeti. Açıklama ve hesap adı dahil değildir: cari ünvanını düzeltmek fişi "değişti" yapmaz.
internal static class FisOzeti
{
    public static string Hesapla(MuhasebeFisi fis)
    {
        var satirlar = fis.Satirlar
            .Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.HesapKodu}|{s.Borc:F2}|{s.Alacak:F2}"))
            .Order(StringComparer.Ordinal);
        return Sha(string.Create(CultureInfo.InvariantCulture, $"{fis.Tur}|{fis.Tarih:yyyy-MM-dd}|{fis.BelgeNo}\n") + string.Join('\n', satirlar));
    }

    public static string Parmak(IEnumerable<MuhasebeFisi> fisler) =>
        Sha(string.Join('\n', fisler.Select(f => $"{f.Kaynak}|{f.KaynakId}|{Hesapla(f)}").Order(StringComparer.Ordinal)));

    private static string Sha(string metin) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(metin)))[..32];
}
