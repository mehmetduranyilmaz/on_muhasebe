using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GirisKilidiBellekte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hatali_giris_sayisi",
                schema: "yonetim",
                table: "kullanici");

            migrationBuilder.DropColumn(
                name: "kilit_bitis_tarihi",
                schema: "yonetim",
                table: "kullanici");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "hatali_giris_sayisi",
                schema: "yonetim",
                table: "kullanici",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "kilit_bitis_tarihi",
                schema: "yonetim",
                table: "kullanici",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
