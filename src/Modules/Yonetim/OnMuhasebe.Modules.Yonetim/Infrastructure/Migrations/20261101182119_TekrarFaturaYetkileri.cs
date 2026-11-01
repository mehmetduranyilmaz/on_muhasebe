using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TekrarFaturaYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hazır rollere yeni yetki: faturayı kesen roller tam, mali müşavir yalnız görüntüleme alır.
            migrationBuilder.Sql("""
                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['fatura.tekrar.goruntule','fatura.tekrar.ekle','fatura.tekrar.duzenle','fatura.tekrar.sil']
                WHERE NOT tam_yetkili_mi AND ad IN ('Kullanıcı', 'Muhasebe', 'Satış');

                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['fatura.tekrar.goruntule']
                WHERE mali_musavir_mi AND NOT ('fatura.tekrar.goruntule' = ANY(yetkiler));
                """);


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
