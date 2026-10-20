using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmaBilgileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "adres",
                schema: "yonetim",
                table: "firma",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eposta",
                schema: "yonetim",
                table: "firma",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "il",
                schema: "yonetim",
                table: "firma",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ilce",
                schema: "yonetim",
                table: "firma",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telefon",
                schema: "yonetim",
                table: "firma",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adres",
                schema: "yonetim",
                table: "firma");

            migrationBuilder.DropColumn(
                name: "eposta",
                schema: "yonetim",
                table: "firma");

            migrationBuilder.DropColumn(
                name: "il",
                schema: "yonetim",
                table: "firma");

            migrationBuilder.DropColumn(
                name: "ilce",
                schema: "yonetim",
                table: "firma");

            migrationBuilder.DropColumn(
                name: "telefon",
                schema: "yonetim",
                table: "firma");
        }
    }
}
