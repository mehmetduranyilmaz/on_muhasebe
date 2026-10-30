using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankaHesapGenisletme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "hesap_kesim_gunu",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pos_aktarilacak_hesap_id",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pos_batch_no",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pos_calisma_sekli",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pos_gun_sayisi",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pos_gun_tipi",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pos_komisyon_orani",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pos_terminal_no",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "son_odeme_gunu",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sube_id",
                schema: "kasabanka",
                table: "kasa_hesabi",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sube",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    banka_adi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    portfoy_yoneticisi = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    faks = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sube", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kasa_hesabi_pos_aktarilacak_hesap_id",
                schema: "kasabanka",
                table: "kasa_hesabi",
                column: "pos_aktarilacak_hesap_id");

            migrationBuilder.CreateIndex(
                name: "ix_kasa_hesabi_sube_id",
                schema: "kasabanka",
                table: "kasa_hesabi",
                column: "sube_id");

            migrationBuilder.CreateIndex(
                name: "ix_sube_firma_id_banka_adi",
                schema: "kasabanka",
                table: "sube",
                columns: new[] { "firma_id", "banka_adi" });

            migrationBuilder.AddForeignKey(
                name: "fk_kasa_hesabi_kasa_hesabi_pos_aktarilacak_hesap_id",
                schema: "kasabanka",
                table: "kasa_hesabi",
                column: "pos_aktarilacak_hesap_id",
                principalSchema: "kasabanka",
                principalTable: "kasa_hesabi",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_kasa_hesabi_subeler_sube_id",
                schema: "kasabanka",
                table: "kasa_hesabi",
                column: "sube_id",
                principalSchema: "kasabanka",
                principalTable: "sube",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_kasa_hesabi_kasa_hesabi_pos_aktarilacak_hesap_id",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropForeignKey(
                name: "fk_kasa_hesabi_subeler_sube_id",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropTable(
                name: "sube",
                schema: "kasabanka");

            migrationBuilder.DropIndex(
                name: "ix_kasa_hesabi_pos_aktarilacak_hesap_id",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropIndex(
                name: "ix_kasa_hesabi_sube_id",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "hesap_kesim_gunu",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "pos_aktarilacak_hesap_id",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "pos_batch_no",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "pos_calisma_sekli",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "pos_gun_sayisi",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "pos_gun_tipi",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "pos_komisyon_orani",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "pos_terminal_no",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "son_odeme_gunu",
                schema: "kasabanka",
                table: "kasa_hesabi");

            migrationBuilder.DropColumn(
                name: "sube_id",
                schema: "kasabanka",
                table: "kasa_hesabi");
        }
    }
}
