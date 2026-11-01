using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AracYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hazır rollere yeni modülün yetkileri: tüm modülleri kapsayan roller tam, mali müşavir yalnız görüntüleme alır.
            migrationBuilder.Sql("""
                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['arac.kart.goruntule','arac.kart.ekle','arac.kart.duzenle','arac.kart.sil','arac.islem.goruntule','arac.islem.ekle','arac.islem.duzenle','arac.islem.sil','arac.rapor.goruntule']
                WHERE NOT tam_yetkili_mi AND ad IN ('Kullanıcı', 'Muhasebe');

                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['arac.kart.goruntule','arac.islem.goruntule','arac.rapor.goruntule']
                WHERE mali_musavir_mi AND NOT ('arac.kart.goruntule' = ANY(yetkiler));
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
