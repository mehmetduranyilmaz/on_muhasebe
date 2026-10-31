using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Cari.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SiparisAvansi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "avans_mi",
                schema: "cari",
                table: "cari_hareket",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avans_mi",
                schema: "cari",
                table: "cari_hareket");
        }
    }
}
