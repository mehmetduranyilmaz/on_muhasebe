using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Tevkifat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "tevkif_edilen_kdv",
                schema: "fatura",
                table: "fatura_satiri",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "tevkifat_kodu",
                schema: "fatura",
                table: "fatura_satiri",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tevkifat_pay",
                schema: "fatura",
                table: "fatura_satiri",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tevkifat_payda",
                schema: "fatura",
                table: "fatura_satiri",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "odenecek_tutar",
                schema: "fatura",
                table: "fatura",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "stopaj_orani",
                schema: "fatura",
                table: "fatura",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "stopaj_tutari",
                schema: "fatura",
                table: "fatura",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "tevkif_edilen_kdv_toplami",
                schema: "fatura",
                table: "fatura",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tevkif_edilen_kdv",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "tevkifat_kodu",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "tevkifat_pay",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "tevkifat_payda",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "odenecek_tutar",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "stopaj_orani",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "stopaj_tutari",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "tevkif_edilen_kdv_toplami",
                schema: "fatura",
                table: "fatura");
        }
    }
}
