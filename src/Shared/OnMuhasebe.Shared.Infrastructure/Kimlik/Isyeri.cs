using OnMuhasebe.Shared.Contracts.Isyerleri;
using OnMuhasebe.Shared.Domain.Results;

namespace OnMuhasebe.Shared.Infrastructure.Kimlik;

// Firmanın işyerleri (Yönetim modülü sağlar; önbellekli). Pasifler de listelenir: eski belgeler onları gösterir.
public interface IIsyeriSorgusu
{
    Task<IReadOnlyList<IsyeriOzeti>> FirmaninAsync(Guid firmaId, CancellationToken cancellationToken = default);

    Task OnbellektenKaldirAsync(Guid firmaId, CancellationToken cancellationToken = default);
}

public static class IsyeriHatalari
{
    public static readonly Hata Bulunamadi = Hata.Bulunamadi("isyeri_bulunamadi", "İşyeri bulunamadı.");
    public static readonly Hata Pasif = Hata.IsKurali("isyeri_pasif", "Bu işyeri kapatılmış; yeni kayıt açılamaz.");
    public static readonly Hata Yetkisiz = Hata.Yetki("isyeri_yetkisiz", "Bu işyerinde işlem yapma yetkiniz yok.");
    public static readonly Hata Yok = Hata.IsKurali("isyeri_yok", "İşlem yapabileceğiniz aktif bir işyeri yok.");
}

// Yeni kaydın işyerini belirler ve denetler: istenen verilmezse kullanıcının varsayılanı, o yoksa merkez, o da izinli değilse
// izinli ilk aktif işyeri. Verilen işyeri firmanın, aktif ve kullanıcının girebildiği bir işyeri olmalıdır.
public sealed class IsyeriSecici(IIsyeriSorgusu sorgu, IYetkiDenetimi yetkiDenetimi, IMevcutKullanici mevcutKullanici)
{
    public async Task<Sonuc<Guid>> BelirleAsync(Guid? istenen, CancellationToken cancellationToken = default)
    {
        var isyerleri = await sorgu.FirmaninAsync(mevcutKullanici.FirmaId!.Value, cancellationToken);
        var yetkiler = await yetkiDenetimi.GetirAsync(cancellationToken);
        bool Girebilir(IsyeriOzeti i) => yetkiler.TamYetkiliMi || yetkiler.IsyerineGirebilirMi(i.Id);

        if (istenen is { } id)
        {
            return isyerleri.FirstOrDefault(i => i.Id == id) switch
            {
                null => IsyeriHatalari.Bulunamadi,
                { AktifMi: false } => IsyeriHatalari.Pasif,
                var i when !Girebilir(i) => IsyeriHatalari.Yetkisiz,
                var i => i.Id
            };
        }

        var aktifler = isyerleri.Where(i => i.AktifMi && Girebilir(i)).ToList();
        return (aktifler.FirstOrDefault(i => i.Id == yetkiler.VarsayilanIsyeriId) ?? aktifler.FirstOrDefault(i => i.MerkezMi) ?? aktifler.FirstOrDefault()) is { } secilen
            ? secilen.Id
            : IsyeriHatalari.Yok;
    }

    // Firma geneli varsayılan kayıtlar (ilk depo, ilk kasa) kullanıcıdan bağımsız olarak merkeze bağlanır.
    public async Task<Guid> MerkezAsync(CancellationToken cancellationToken = default) =>
        (await sorgu.FirmaninAsync(mevcutKullanici.FirmaId!.Value, cancellationToken)).First(i => i.MerkezMi).Id;

    // Var olan kaydın işyeri değişiyorsa yeni işyeri denetlenir; değişmiyorsa (pasifleşmiş olsa da) korunur.
    public async Task<Sonuc<Guid>> DegisiklikAsync(Guid mevcut, Guid? istenen, CancellationToken cancellationToken = default) =>
        istenen is null || istenen == mevcut ? mevcut : await BelirleAsync(istenen, cancellationToken);
}
