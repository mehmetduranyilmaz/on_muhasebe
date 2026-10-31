using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GiderPusulasiSmm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.AddColumn<string>(
                name: "gider_pusulasi_seri",
                schema: "fatura",
                table: "fatura_ayari",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "GPS");

            migrationBuilder.CreateIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura",
                columns: new[] { "firma_id", "numara" },
                unique: true,
                filter: "numara IS NOT NULL AND tur IN ('Satis', 'AlistanIade', 'GiderPusulasi')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "gider_pusulasi_seri",
                schema: "fatura",
                table: "fatura_ayari");

            migrationBuilder.CreateIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura",
                columns: new[] { "firma_id", "numara" },
                unique: true,
                filter: "numara IS NOT NULL AND tur IN ('Satis', 'AlistanIade')");
        }
    }
}
