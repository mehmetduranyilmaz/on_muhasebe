using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TeslimatAdresiVeVarsayilanAciklama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "varsayilan_aciklama",
                schema: "fatura",
                table: "fatura_ayari",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teslimat_adres",
                schema: "fatura",
                table: "fatura",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teslimat_alici_adi",
                schema: "fatura",
                table: "fatura",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teslimat_baslik",
                schema: "fatura",
                table: "fatura",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teslimat_il",
                schema: "fatura",
                table: "fatura",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teslimat_ilce",
                schema: "fatura",
                table: "fatura",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teslimat_posta_kodu",
                schema: "fatura",
                table: "fatura",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teslimat_telefon",
                schema: "fatura",
                table: "fatura",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teslimat_ulke",
                schema: "fatura",
                table: "fatura",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "varsayilan_aciklama",
                schema: "fatura",
                table: "fatura_ayari");

            migrationBuilder.DropColumn(
                name: "teslimat_adres",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "teslimat_alici_adi",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "teslimat_baslik",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "teslimat_il",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "teslimat_ilce",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "teslimat_posta_kodu",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "teslimat_telefon",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "teslimat_ulke",
                schema: "fatura",
                table: "fatura");
        }
    }
}
