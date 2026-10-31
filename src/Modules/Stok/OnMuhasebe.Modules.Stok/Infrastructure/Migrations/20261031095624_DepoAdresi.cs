using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Stok.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DepoAdresi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "adres",
                schema: "stok",
                table: "depo",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "il",
                schema: "stok",
                table: "depo",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ilce",
                schema: "stok",
                table: "depo",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adres",
                schema: "stok",
                table: "depo");

            migrationBuilder.DropColumn(
                name: "il",
                schema: "stok",
                table: "depo");

            migrationBuilder.DropColumn(
                name: "ilce",
                schema: "stok",
                table: "depo");
        }
    }
}
