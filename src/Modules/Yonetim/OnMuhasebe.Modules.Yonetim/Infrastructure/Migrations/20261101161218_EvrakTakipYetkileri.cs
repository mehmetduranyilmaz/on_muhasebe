using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EvrakTakipYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hazır rollere yeni modülün yetkileri: tüm modülleri kapsayan roller tam, mali müşavir yalnız görüntüleme alır.
            migrationBuilder.Sql("""
                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['evraktakip.kayit.goruntule','evraktakip.kayit.ekle','evraktakip.kayit.duzenle','evraktakip.kayit.sil']
                WHERE NOT tam_yetkili_mi AND ad IN ('Kullanıcı', 'Muhasebe');

                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['evraktakip.kayit.goruntule']
                WHERE mali_musavir_mi AND NOT ('evraktakip.kayit.goruntule' = ANY(yetkiler));
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
