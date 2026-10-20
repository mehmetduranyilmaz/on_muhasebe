using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Stok.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "stok");

            migrationBuilder.CreateTable(
                name: "depo",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    varsayilan_mi = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_depo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stok_ayari",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    maliyet_yontemi = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    eksi_stok_davranisi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stok_ayari", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stok_grubu",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stok_grubu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stok_karti",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ad = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    tur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    temel_birim = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    barkod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    kdv_orani = table.Column<int>(type: "integer", nullable: false),
                    alis_fiyati = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    satis_fiyati = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    grup_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kritik_seviye = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    silindi_mi = table.Column<bool>(type: "boolean", nullable: false),
                    silinme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stok_karti", x => x.id);
                    table.ForeignKey(
                        name: "fk_stok_karti_stok_grubu_grup_id",
                        column: x => x.grup_id,
                        principalSchema: "stok",
                        principalTable: "stok_grubu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stok_hareket",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stok_karti_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    giris_miktari = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cikis_miktari = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    girilen_birim = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    girilen_miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    birim_fiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    belge_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kaynak_belge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iptal_edilen_hareket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iptal_hareket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stok_hareket", x => x.id);
                    table.ForeignKey(
                        name: "fk_stok_hareket_depo_depo_id",
                        column: x => x.depo_id,
                        principalSchema: "stok",
                        principalTable: "depo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stok_hareket_stok_kartlari_stok_karti_id",
                        column: x => x.stok_karti_id,
                        principalSchema: "stok",
                        principalTable: "stok_karti",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stok_karti_birimi",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stok_karti_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birim_kodu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    katsayi = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    barkod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stok_karti_birimi", x => x.id);
                    table.ForeignKey(
                        name: "fk_stok_karti_birimi_stok_kartlari_stok_karti_id",
                        column: x => x.stok_karti_id,
                        principalSchema: "stok",
                        principalTable: "stok_karti",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_depo_firma_id_ad",
                schema: "stok",
                table: "depo",
                columns: new[] { "firma_id", "ad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stok_ayari_firma_id",
                schema: "stok",
                table: "stok_ayari",
                column: "firma_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stok_grubu_firma_id_ad",
                schema: "stok",
                table: "stok_grubu",
                columns: new[] { "firma_id", "ad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_depo_id",
                schema: "stok",
                table: "stok_hareket",
                column: "depo_id");

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_firma_id_depo_id",
                schema: "stok",
                table: "stok_hareket",
                columns: new[] { "firma_id", "depo_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_firma_id_stok_karti_id_tarih",
                schema: "stok",
                table: "stok_hareket",
                columns: new[] { "firma_id", "stok_karti_id", "tarih" })
                .Annotation("Npgsql:IndexInclude", new[] { "depo_id", "giris_miktari", "cikis_miktari", "birim_fiyat", "tur" });

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_kaynak_belge_id",
                schema: "stok",
                table: "stok_hareket",
                column: "kaynak_belge_id");

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_stok_karti_id",
                schema: "stok",
                table: "stok_hareket",
                column: "stok_karti_id");

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_transfer_id",
                schema: "stok",
                table: "stok_hareket",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_stok_karti_firma_id_ad",
                schema: "stok",
                table: "stok_karti",
                columns: new[] { "firma_id", "ad" });

            migrationBuilder.CreateIndex(
                name: "ix_stok_karti_firma_id_barkod",
                schema: "stok",
                table: "stok_karti",
                columns: new[] { "firma_id", "barkod" });

            migrationBuilder.CreateIndex(
                name: "ix_stok_karti_firma_id_kod",
                schema: "stok",
                table: "stok_karti",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stok_karti_grup_id",
                schema: "stok",
                table: "stok_karti",
                column: "grup_id");

            migrationBuilder.CreateIndex(
                name: "ix_stok_karti_birimi_barkod",
                schema: "stok",
                table: "stok_karti_birimi",
                column: "barkod");

            migrationBuilder.CreateIndex(
                name: "ix_stok_karti_birimi_stok_karti_id_birim_kodu",
                schema: "stok",
                table: "stok_karti_birimi",
                columns: new[] { "stok_karti_id", "birim_kodu" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stok_ayari",
                schema: "stok");

            migrationBuilder.DropTable(
                name: "stok_hareket",
                schema: "stok");

            migrationBuilder.DropTable(
                name: "stok_karti_birimi",
                schema: "stok");

            migrationBuilder.DropTable(
                name: "depo",
                schema: "stok");

            migrationBuilder.DropTable(
                name: "stok_karti",
                schema: "stok");

            migrationBuilder.DropTable(
                name: "stok_grubu",
                schema: "stok");
        }
    }
}
