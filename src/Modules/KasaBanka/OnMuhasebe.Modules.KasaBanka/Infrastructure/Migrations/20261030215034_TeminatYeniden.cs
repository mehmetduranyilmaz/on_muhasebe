using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TeminatYeniden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "taraf_adi",
                schema: "kasabanka",
                table: "teminat");

            migrationBuilder.AddColumn<string>(
                name: "banka_adi",
                schema: "kasabanka",
                table: "teminat",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cari_hesap_id",
                schema: "kasabanka",
                table: "teminat",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "detaylar",
                schema: "kasabanka",
                table: "teminat",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "komisyon_donemi",
                schema: "kasabanka",
                table: "teminat",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "komisyon_orani",
                schema: "kasabanka",
                table: "teminat",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "komisyon_tutari",
                schema: "kasabanka",
                table: "teminat",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_teminat_firma_id_cari_hesap_id",
                schema: "kasabanka",
                table: "teminat",
                columns: new[] { "firma_id", "cari_hesap_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_teminat_firma_id_cari_hesap_id",
                schema: "kasabanka",
                table: "teminat");

            migrationBuilder.DropColumn(
                name: "banka_adi",
                schema: "kasabanka",
                table: "teminat");

            migrationBuilder.DropColumn(
                name: "cari_hesap_id",
                schema: "kasabanka",
                table: "teminat");

            migrationBuilder.DropColumn(
                name: "detaylar",
                schema: "kasabanka",
                table: "teminat");

            migrationBuilder.DropColumn(
                name: "komisyon_donemi",
                schema: "kasabanka",
                table: "teminat");

            migrationBuilder.DropColumn(
                name: "komisyon_orani",
                schema: "kasabanka",
                table: "teminat");

            migrationBuilder.DropColumn(
                name: "komisyon_tutari",
                schema: "kasabanka",
                table: "teminat");

            migrationBuilder.AddColumn<string>(
                name: "taraf_adi",
                schema: "kasabanka",
                table: "teminat",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");
        }
    }
}
