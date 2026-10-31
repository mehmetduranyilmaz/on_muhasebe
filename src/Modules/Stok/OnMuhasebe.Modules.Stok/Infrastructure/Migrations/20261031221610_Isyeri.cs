using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Stok.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Isyeri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "isyeri_id",
                schema: "stok",
                table: "depo",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Mevcut kayıtlar firmanın merkez işyerine bağlanır.

            migrationBuilder.Sql("""
                UPDATE stok.depo d SET isyeri_id = i.id FROM yonetim.isyeri i WHERE i.firma_id = d.firma_id AND i.merkez_mi;
                ALTER TABLE stok.depo ALTER COLUMN isyeri_id DROP DEFAULT;

                """);


            migrationBuilder.CreateIndex(
                name: "ix_depo_firma_id_isyeri_id",
                schema: "stok",
                table: "depo",
                columns: new[] { "firma_id", "isyeri_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_depo_firma_id_isyeri_id",
                schema: "stok",
                table: "depo");

            migrationBuilder.DropColumn(
                name: "isyeri_id",
                schema: "stok",
                table: "depo");
        }
    }
}
