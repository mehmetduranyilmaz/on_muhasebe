using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Stok.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FiyatListesi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fiyat_listesi",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    kdv_dahil_mi = table.Column<bool>(type: "boolean", nullable: false),
                    baslangic_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    bitis_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fiyat_listesi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fiyat_listesi_atamasi",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiyat_listesi_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    hedef_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fiyat_listesi_atamasi", x => x.id);
                    table.ForeignKey(
                        name: "fk_fiyat_listesi_atamasi_fiyat_listeleri_fiyat_listesi_id",
                        column: x => x.fiyat_listesi_id,
                        principalSchema: "stok",
                        principalTable: "fiyat_listesi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fiyat_listesi_satiri",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiyat_listesi_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stok_karti_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    fiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    iskonto = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fiyat_listesi_satiri", x => x.id);
                    table.ForeignKey(
                        name: "fk_fiyat_listesi_satiri_fiyat_listesi_fiyat_listesi_id",
                        column: x => x.fiyat_listesi_id,
                        principalSchema: "stok",
                        principalTable: "fiyat_listesi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fiyat_listesi_satiri_stok_kartlari_stok_karti_id",
                        column: x => x.stok_karti_id,
                        principalSchema: "stok",
                        principalTable: "stok_karti",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fiyat_listesi_firma_id_ad",
                schema: "stok",
                table: "fiyat_listesi",
                columns: new[] { "firma_id", "ad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fiyat_listesi_atamasi_fiyat_listesi_id_tur_hedef_id",
                schema: "stok",
                table: "fiyat_listesi_atamasi",
                columns: new[] { "fiyat_listesi_id", "tur", "hedef_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fiyat_listesi_atamasi_hedef_id",
                schema: "stok",
                table: "fiyat_listesi_atamasi",
                column: "hedef_id");

            migrationBuilder.CreateIndex(
                name: "ix_fiyat_listesi_satiri_fiyat_listesi_id_stok_karti_id_min_mik",
                schema: "stok",
                table: "fiyat_listesi_satiri",
                columns: new[] { "fiyat_listesi_id", "stok_karti_id", "min_miktar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fiyat_listesi_satiri_stok_karti_id",
                schema: "stok",
                table: "fiyat_listesi_satiri",
                column: "stok_karti_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiyat_listesi_atamasi",
                schema: "stok");

            migrationBuilder.DropTable(
                name: "fiyat_listesi_satiri",
                schema: "stok");

            migrationBuilder.DropTable(
                name: "fiyat_listesi",
                schema: "stok");
        }
    }
}
