using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Stok.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerakendeFiyati : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "perakende_fiyati",
                schema: "stok",
                table: "stok_karti",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "perakende_fiyati",
                schema: "stok",
                table: "stok_karti");
        }
    }
}
