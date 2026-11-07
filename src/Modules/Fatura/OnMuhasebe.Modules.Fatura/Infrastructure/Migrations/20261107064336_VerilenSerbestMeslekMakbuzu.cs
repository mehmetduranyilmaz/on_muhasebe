using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VerilenSerbestMeslekMakbuzu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.AddColumn<string>(
                name: "serbest_meslek_seri",
                schema: "fatura",
                table: "fatura_ayari",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SMM");

            migrationBuilder.AlterColumn<string>(
                name: "tur",
                schema: "fatura",
                table: "fatura",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura",
                columns: new[] { "firma_id", "numara" },
                unique: true,
                filter: "numara IS NOT NULL AND tur IN ('Satis', 'AlistanIade', 'GiderPusulasi', 'VerilenSerbestMeslekMakbuzu')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "serbest_meslek_seri",
                schema: "fatura",
                table: "fatura_ayari");

            migrationBuilder.AlterColumn<string>(
                name: "tur",
                schema: "fatura",
                table: "fatura",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura",
                columns: new[] { "firma_id", "numara" },
                unique: true,
                filter: "numara IS NOT NULL AND tur IN ('Satis', 'AlistanIade', 'GiderPusulasi')");
        }
    }
}
