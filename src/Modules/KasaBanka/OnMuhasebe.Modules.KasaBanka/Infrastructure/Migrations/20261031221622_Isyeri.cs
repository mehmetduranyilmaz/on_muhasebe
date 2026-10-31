using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Isyeri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "isyeri_id",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "isyeri_id",
                schema: "kasabanka",
                table: "demirbas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Mevcut kayıtlar firmanın merkez işyerine bağlanır.

            migrationBuilder.Sql("""
                -- Kasa ve POS işyerinindir; banka ve kredi kartı firma geneli kalır.
                UPDATE kasabanka.kasa_hesabi h SET isyeri_id = i.id FROM yonetim.isyeri i WHERE i.firma_id = h.firma_id AND i.merkez_mi AND h.tur IN ('Kasa', 'Pos');
                UPDATE kasabanka.demirbas d SET isyeri_id = i.id FROM yonetim.isyeri i WHERE i.firma_id = d.firma_id AND i.merkez_mi;
                ALTER TABLE kasabanka.demirbas ALTER COLUMN isyeri_id DROP DEFAULT;

                """);


            migrationBuilder.CreateIndex(
                name: "ix_kasa_hesabi_firma_id_isyeri_id",
                schema: "kasabanka",
                table: "kasa_hesabi",
                columns: new[] { "firma_id", "isyeri_id" });

            migrationBuilder.CreateIndex(
                name: "ix_demirbas_firma_id_isyeri_id",
                schema: "kasabanka",
                table: "demirbas",
                columns: new[] { "firma_id", "isyeri_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_kasa_hesabi_firma_id_isyeri_id",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropIndex(
                name: "ix_demirbas_firma_id_isyeri_id",
                schema: "kasabanka",
                table: "demirbas");

            migrationBuilder.DropColumn(
                name: "isyeri_id",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "isyeri_id",
                schema: "kasabanka",
                table: "demirbas");
        }
    }
}
