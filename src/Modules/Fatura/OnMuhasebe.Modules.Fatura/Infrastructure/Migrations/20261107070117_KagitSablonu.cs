using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KagitSablonu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kagit_sablonu",
                schema: "fatura",
                table: "fatura_ayari",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Standart");

            migrationBuilder.AddColumn<bool>(
                name: "kagitta_banka_bilgisi",
                schema: "fatura",
                table: "fatura_ayari",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "kagitta_imza_alani",
                schema: "fatura",
                table: "fatura_ayari",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kagit_sablonu",
                schema: "fatura",
                table: "fatura_ayari");

            migrationBuilder.DropColumn(
                name: "kagitta_banka_bilgisi",
                schema: "fatura",
                table: "fatura_ayari");

            migrationBuilder.DropColumn(
                name: "kagitta_imza_alani",
                schema: "fatura",
                table: "fatura_ayari");
        }
    }
}
