namespace OnMuhasebe.Shared.Domain.Abstractions;

// İşyerine bağlı kayıt (belge, depo, kasa). Varlıkta "IsyeriId" adlı Guid ya da Guid? özellik bulunur; Guid? boşsa kayıt firma
// genelidir (ör. işyerine bağlanmamış banka hesabı). İşyeri kısıtlı kullanıcı yalnızca izinli işyerlerinin kayıtlarını görür.
public interface IIsyeriVarligi;
