using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "yonetim");

            migrationBuilder.CreateTable(
                name: "firma",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unvan = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    vergi_no = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    vergi_dairesi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firma", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kullanici",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    soyad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    eposta = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    sifre_ozeti = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    sistem_yoneticisi_mi = table.Column<bool>(type: "boolean", nullable: false),
                    hatali_giris_sayisi = table.Column<int>(type: "integer", nullable: false),
                    kilit_bitis_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    son_firma_id = table.Column<Guid>(type: "uuid", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kullanici", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "paket",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tum_moduller_mi = table.Column<bool>(type: "boolean", nullable: false),
                    moduller = table.Column<List<string>>(type: "text[]", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paket", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "firma_kullanicisi",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("pk_firma_kullanicisi", x => x.id);
                    table.ForeignKey(
                        name: "fk_firma_kullanicisi_firma_firma_id",
                        column: x => x.firma_id,
                        principalSchema: "yonetim",
                        principalTable: "firma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_firma_kullanicisi_kullanicilar_kullanici_id",
                        column: x => x.kullanici_id,
                        principalSchema: "yonetim",
                        principalTable: "kullanici",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_ozeti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bitis_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    iptal_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    yerine_gecen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_kullanici_kullanici_id",
                        column: x => x.kullanici_id,
                        principalSchema: "yonetim",
                        principalTable: "kullanici",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "firma_lisansi",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    baslangic_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    bitis_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ek_moduller = table.Column<List<string>>(type: "text[]", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firma_lisansi", x => x.id);
                    table.ForeignKey(
                        name: "fk_firma_lisansi_firma_firma_id",
                        column: x => x.firma_id,
                        principalSchema: "yonetim",
                        principalTable: "firma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_firma_lisansi_paketler_paket_id",
                        column: x => x.paket_id,
                        principalSchema: "yonetim",
                        principalTable: "paket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Elle eklendi: başlangıç paketleri (bkz. PaketConfiguration).
            migrationBuilder.InsertData(
                schema: "yonetim",
                table: "paket",
                columns: new[] { "id", "kod", "ad", "tum_moduller_mi", "moduller", "olusturma_tarihi" },
                values: new object[,]
                {
                    {
                        new Guid("019a0000-0000-7000-8000-000000000001"), "deneme", "Deneme", true,
                        Array.Empty<string>(), new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        new Guid("019a0000-0000-7000-8000-000000000002"), "basic", "Basic", false,
                        new[] { "cari", "stok", "fatura", "kasabanka" }, new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
                    }
                });

            migrationBuilder.CreateIndex(
                name: "ix_firma_kullanicisi_firma_id_kullanici_id",
                schema: "yonetim",
                table: "firma_kullanicisi",
                columns: new[] { "firma_id", "kullanici_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_firma_kullanicisi_kullanici_id",
                schema: "yonetim",
                table: "firma_kullanicisi",
                column: "kullanici_id");

            migrationBuilder.CreateIndex(
                name: "ix_firma_lisansi_firma_id",
                schema: "yonetim",
                table: "firma_lisansi",
                column: "firma_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_firma_lisansi_paket_id",
                schema: "yonetim",
                table: "firma_lisansi",
                column: "paket_id");

            migrationBuilder.CreateIndex(
                name: "ix_kullanici_eposta",
                schema: "yonetim",
                table: "kullanici",
                column: "eposta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_paket_kod",
                schema: "yonetim",
                table: "paket",
                column: "kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_kullanici_id",
                schema: "yonetim",
                table: "refresh_token",
                column: "kullanici_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_token_ozeti",
                schema: "yonetim",
                table: "refresh_token",
                column: "token_ozeti",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "firma_kullanicisi",
                schema: "yonetim");

            migrationBuilder.DropTable(
                name: "firma_lisansi",
                schema: "yonetim");

            migrationBuilder.DropTable(
                name: "refresh_token",
                schema: "yonetim");

            migrationBuilder.DropTable(
                name: "firma",
                schema: "yonetim");

            migrationBuilder.DropTable(
                name: "paket",
                schema: "yonetim");

            migrationBuilder.DropTable(
                name: "kullanici",
                schema: "yonetim");
        }
    }
}
