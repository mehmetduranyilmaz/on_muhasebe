using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Stok.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StokKartiIkinciAdTevkifat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ad2",
                schema: "stok",
                table: "stok_karti",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "varsayilan_tevkifat_kodu",
                schema: "stok",
                table: "stok_karti",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ad2",
                schema: "stok",
                table: "stok_karti");

            migrationBuilder.DropColumn(
                name: "varsayilan_tevkifat_kodu",
                schema: "stok",
                table: "stok_karti");
        }
    }
}
