using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Isyeri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "isyeri_id",
                schema: "fatura",
                table: "teklif_siparis",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "isyeri_id",
                schema: "fatura",
                table: "irsaliye",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "isyeri_id",
                schema: "fatura",
                table: "fatura",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Mevcut kayıtlar firmanın merkez işyerine bağlanır.

            migrationBuilder.Sql("""
                UPDATE fatura.fatura b SET isyeri_id = i.id FROM yonetim.isyeri i WHERE i.firma_id = b.firma_id AND i.merkez_mi;
                ALTER TABLE fatura.fatura ALTER COLUMN isyeri_id DROP DEFAULT;
                UPDATE fatura.irsaliye b SET isyeri_id = i.id FROM yonetim.isyeri i WHERE i.firma_id = b.firma_id AND i.merkez_mi;
                ALTER TABLE fatura.irsaliye ALTER COLUMN isyeri_id DROP DEFAULT;
                UPDATE fatura.teklif_siparis b SET isyeri_id = i.id FROM yonetim.isyeri i WHERE i.firma_id = b.firma_id AND i.merkez_mi;
                ALTER TABLE fatura.teklif_siparis ALTER COLUMN isyeri_id DROP DEFAULT;

                """);


            migrationBuilder.CreateIndex(
                name: "ix_teklif_siparis_firma_id_isyeri_id_tarih",
                schema: "fatura",
                table: "teklif_siparis",
                columns: new[] { "firma_id", "isyeri_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_irsaliye_firma_id_isyeri_id_tarih",
                schema: "fatura",
                table: "irsaliye",
                columns: new[] { "firma_id", "isyeri_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_fatura_firma_id_isyeri_id_tarih",
                schema: "fatura",
                table: "fatura",
                columns: new[] { "firma_id", "isyeri_id", "tarih" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_teklif_siparis_firma_id_isyeri_id_tarih",
                schema: "fatura",
                table: "teklif_siparis");

            migrationBuilder.DropIndex(
                name: "ix_irsaliye_firma_id_isyeri_id_tarih",
                schema: "fatura",
                table: "irsaliye");

            migrationBuilder.DropIndex(
                name: "ix_fatura_firma_id_isyeri_id_tarih",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "isyeri_id",
                schema: "fatura",
                table: "teklif_siparis");

            migrationBuilder.DropColumn(
                name: "isyeri_id",
                schema: "fatura",
                table: "irsaliye");

            migrationBuilder.DropColumn(
                name: "isyeri_id",
                schema: "fatura",
                table: "fatura");
        }
    }
}
