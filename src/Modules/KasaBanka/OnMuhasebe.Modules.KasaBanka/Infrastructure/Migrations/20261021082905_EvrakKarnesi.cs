using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EvrakKarnesi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "karne_id",
                schema: "kasabanka",
                table: "evrak",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "karne_uyari_esigi",
                schema: "kasabanka",
                table: "cek_senet_ayari",
                type: "integer",
                nullable: false,
                defaultValue: 5); // Elle: mevcut firmalar varsayılan eşikle başlasın.

            migrationBuilder.CreateTable(
                name: "evrak_karnesi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    kendi_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    onek = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    baslangic_no = table.Column<long>(type: "bigint", nullable: false),
                    hane = table.Column<int>(type: "integer", nullable: false),
                    yaprak_sayisi = table.Column<int>(type: "integer", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evrak_karnesi", x => x.id);
                    table.ForeignKey(
                        name: "fk_evrak_karnesi_hesaplar_kendi_hesap_id",
                        column: x => x.kendi_hesap_id,
                        principalSchema: "kasabanka",
                        principalTable: "kasa_hesabi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "karne_yaprak_iptali",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    karne_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seri_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    neden = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_karne_yaprak_iptali", x => x.id);
                    table.ForeignKey(
                        name: "fk_karne_yaprak_iptali_evrak_karnesi_karne_id",
                        column: x => x.karne_id,
                        principalSchema: "kasabanka",
                        principalTable: "evrak_karnesi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evrak_karne_id_seri_no",
                schema: "kasabanka",
                table: "evrak",
                columns: new[] { "karne_id", "seri_no" },
                filter: "karne_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_evrak_karnesi_firma_id_tur_kendi_hesap_id",
                schema: "kasabanka",
                table: "evrak_karnesi",
                columns: new[] { "firma_id", "tur", "kendi_hesap_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evrak_karnesi_kendi_hesap_id",
                schema: "kasabanka",
                table: "evrak_karnesi",
                column: "kendi_hesap_id");

            migrationBuilder.CreateIndex(
                name: "ix_karne_yaprak_iptali_karne_id_seri_no",
                schema: "kasabanka",
                table: "karne_yaprak_iptali",
                columns: new[] { "karne_id", "seri_no" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_evrak_karneler_karne_id",
                schema: "kasabanka",
                table: "evrak",
                column: "karne_id",
                principalSchema: "kasabanka",
                principalTable: "evrak_karnesi",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_evrak_karneler_karne_id",
                schema: "kasabanka",
                table: "evrak");

            migrationBuilder.DropTable(
                name: "karne_yaprak_iptali",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "evrak_karnesi",
                schema: "kasabanka");

            migrationBuilder.DropIndex(
                name: "ix_evrak_karne_id_seri_no",
                schema: "kasabanka",
                table: "evrak");

            migrationBuilder.DropColumn(
                name: "karne_id",
                schema: "kasabanka",
                table: "evrak");

            migrationBuilder.DropColumn(
                name: "karne_uyari_esigi",
                schema: "kasabanka",
                table: "cek_senet_ayari");
        }
    }
}
