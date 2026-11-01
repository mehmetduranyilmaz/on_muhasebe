using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DepolamaKotasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "depolama_kotasi_mb",
                schema: "yonetim",
                table: "paket",
                type: "integer",
                nullable: true);

            // Mevcut paketlere başlangıç kotası; sistem yöneticisi paket ekranından değiştirir.
            migrationBuilder.Sql("UPDATE yonetim.paket SET depolama_kotasi_mb = CASE WHEN kod = 'deneme' THEN 500 ELSE 2048 END;");

            // Mevcut rollere dosya yetkisi: işlem yapabilen rol dosya da ekler ve siler, diğerleri (mali müşavir, salt okur) yalnız görür.
            migrationBuilder.Sql("""
                UPDATE yonetim.firma_rolu r SET yetkiler = r.yetkiler || ARRAY['dosya.ek.goruntule','dosya.ek.ekle','dosya.ek.sil']
                WHERE NOT r.tam_yetkili_mi AND NOT r.mali_musavir_mi AND EXISTS (SELECT 1 FROM unnest(r.yetkiler) y WHERE y LIKE '%.ekle');

                UPDATE yonetim.firma_rolu r SET yetkiler = r.yetkiler || ARRAY['dosya.ek.goruntule']
                WHERE NOT r.tam_yetkili_mi AND NOT ('dosya.ek.goruntule' = ANY(r.yetkiler));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "depolama_kotasi_mb",
                schema: "yonetim",
                table: "paket");
        }
    }
}
