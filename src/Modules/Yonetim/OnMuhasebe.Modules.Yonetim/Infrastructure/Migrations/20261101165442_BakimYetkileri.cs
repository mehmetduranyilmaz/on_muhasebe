using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BakimYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hazır rollere yeni modülün yetkileri: tüm modülleri kapsayan roller tam, mali müşavir yalnız görüntüleme alır.
            migrationBuilder.Sql("""
                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['bakim.ekipman.goruntule','bakim.ekipman.ekle','bakim.ekipman.duzenle','bakim.ekipman.sil','bakim.isemri.goruntule','bakim.isemri.ekle','bakim.isemri.duzenle','bakim.isemri.sil','bakim.rapor.goruntule']
                WHERE NOT tam_yetkili_mi AND ad IN ('Kullanıcı', 'Muhasebe');

                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['bakim.ekipman.goruntule','bakim.isemri.goruntule','bakim.rapor.goruntule']
                WHERE mali_musavir_mi AND NOT ('bakim.ekipman.goruntule' = ANY(yetkiler));
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
