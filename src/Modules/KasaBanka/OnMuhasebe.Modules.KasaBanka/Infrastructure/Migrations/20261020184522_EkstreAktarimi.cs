using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EkstreAktarimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ekstre_satiri_id",
                schema: "kasabanka",
                table: "kasa_hareketi",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ekstre_aktarimi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dosya_adi = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    bicim = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    icerik = table.Column<byte[]>(type: "bytea", nullable: true),
                    esleme = table.Column<string>(type: "jsonb", nullable: true),
                    baslangic = table.Column<DateOnly>(type: "date", nullable: true),
                    bitis = table.Column<DateOnly>(type: "date", nullable: true),
                    bakiye_uyarisi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tamamlanma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ekstre_aktarimi", x => x.id);
                    table.ForeignKey(
                        name: "fk_ekstre_aktarimi_hesaplar_hesap_id",
                        column: x => x.hesap_id,
                        principalSchema: "kasabanka",
                        principalTable: "kasa_hesabi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ekstre_kurali",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    anahtar_kelime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    yon = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    islem_turu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kategori_id = table.Column<Guid>(type: "uuid", nullable: true),
                    karsi_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kullanim_sayisi = table.Column<int>(type: "integer", nullable: false),
                    son_kullanim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ekstre_kurali", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ekstre_sablonu",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    esleme = table.Column<string>(type: "jsonb", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ekstre_sablonu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ekstre_satiri",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aktarim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    bakiye = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    referans = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    parmak_izi = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    islem_turu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kategori_id = table.Column<Guid>(type: "uuid", nullable: true),
                    karsi_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    eslesen_hareket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    islem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kural_id = table.Column<Guid>(type: "uuid", nullable: true),
                    oneri_kaynagi = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    hata = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    elle_duzenlendi = table.Column<bool>(type: "boolean", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ekstre_satiri", x => x.id);
                    table.ForeignKey(
                        name: "fk_ekstre_satiri_ekstre_aktarimi_aktarim_id",
                        column: x => x.aktarim_id,
                        principalSchema: "kasabanka",
                        principalTable: "ekstre_aktarimi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kasa_hareketi_ekstre_satiri_id",
                schema: "kasabanka",
                table: "kasa_hareketi",
                column: "ekstre_satiri_id",
                unique: true,
                filter: "ekstre_satiri_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ekstre_aktarimi_firma_id_hesap_id",
                schema: "kasabanka",
                table: "ekstre_aktarimi",
                columns: new[] { "firma_id", "hesap_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ekstre_aktarimi_hesap_id",
                schema: "kasabanka",
                table: "ekstre_aktarimi",
                column: "hesap_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekstre_kurali_firma_id_anahtar_kelime_yon",
                schema: "kasabanka",
                table: "ekstre_kurali",
                columns: new[] { "firma_id", "anahtar_kelime", "yon" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ekstre_sablonu_firma_id_hesap_id",
                schema: "kasabanka",
                table: "ekstre_sablonu",
                columns: new[] { "firma_id", "hesap_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ekstre_satiri_aktarim_id",
                schema: "kasabanka",
                table: "ekstre_satiri",
                column: "aktarim_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekstre_satiri_eslesen_hareket_id",
                schema: "kasabanka",
                table: "ekstre_satiri",
                column: "eslesen_hareket_id",
                unique: true,
                filter: "eslesen_hareket_id IS NOT NULL AND durum = 'ZatenKayitli'");

            migrationBuilder.CreateIndex(
                name: "ix_ekstre_satiri_hesap_id_parmak_izi",
                schema: "kasabanka",
                table: "ekstre_satiri",
                columns: new[] { "hesap_id", "parmak_izi" },
                unique: true,
                filter: "durum IN ('Aktarildi', 'ZatenKayitli')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ekstre_kurali",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "ekstre_sablonu",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "ekstre_satiri",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "ekstre_aktarimi",
                schema: "kasabanka");

            migrationBuilder.DropIndex(
                name: "ix_kasa_hareketi_ekstre_satiri_id",
                schema: "kasabanka",
                table: "kasa_hareketi");

            migrationBuilder.DropColumn(
                name: "ekstre_satiri_id",
                schema: "kasabanka",
                table: "kasa_hareketi");
        }
    }
}
