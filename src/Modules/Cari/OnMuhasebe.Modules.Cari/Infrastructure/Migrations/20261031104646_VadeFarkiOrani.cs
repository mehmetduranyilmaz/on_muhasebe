using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Cari.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VadeFarkiOrani : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "vade_farki_orani",
                schema: "cari",
                table: "cari_hesap",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "vade_farki_orani",
                schema: "cari",
                table: "cari_hesap");
        }
    }
}
