using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kasabanka");

            migrationBuilder.CreateTable(
                name: "gelir_gider_kategorisi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
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
                    table.PrimaryKey("pk_gelir_gider_kategorisi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kasa_hesabi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tur = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    banka_adi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sube = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hesap_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_kasa_hesabi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gelir_gider_kaydi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    islem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    bicim = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    kategori_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kur = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    tutar_tl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    belge_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    iptal_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gelir_gider_kaydi", x => x.id);
                    table.ForeignKey(
                        name: "fk_gelir_gider_kaydi_gelir_gider_kategorisi_kategori_id",
                        column: x => x.kategori_id,
                        principalSchema: "kasabanka",
                        principalTable: "gelir_gider_kategorisi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kasa_hareketi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    islem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    tur = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    giris = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cikis = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kur = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    giris_tl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cikis_tl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    belge_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kategori_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_kasa_hareketi", x => x.id);
                    table.ForeignKey(
                        name: "fk_kasa_hareketi_hesaplar_hesap_id",
                        column: x => x.hesap_id,
                        principalSchema: "kasabanka",
                        principalTable: "kasa_hesabi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gelir_gider_kategorisi_firma_id_tur_ad",
                schema: "kasabanka",
                table: "gelir_gider_kategorisi",
                columns: new[] { "firma_id", "tur", "ad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gelir_gider_kaydi_firma_id_tarih",
                schema: "kasabanka",
                table: "gelir_gider_kaydi",
                columns: new[] { "firma_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_gelir_gider_kaydi_islem_id",
                schema: "kasabanka",
                table: "gelir_gider_kaydi",
                column: "islem_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gelir_gider_kaydi_kategori_id",
                schema: "kasabanka",
                table: "gelir_gider_kaydi",
                column: "kategori_id");

            migrationBuilder.CreateIndex(
                name: "ix_kasa_hareketi_firma_id_hesap_id_tarih",
                schema: "kasabanka",
                table: "kasa_hareketi",
                columns: new[] { "firma_id", "hesap_id", "tarih" })
                .Annotation("Npgsql:IndexInclude", new[] { "giris", "cikis", "giris_tl", "cikis_tl" });

            migrationBuilder.CreateIndex(
                name: "ix_kasa_hareketi_hesap_id",
                schema: "kasabanka",
                table: "kasa_hareketi",
                column: "hesap_id");

            migrationBuilder.CreateIndex(
                name: "ix_kasa_hareketi_islem_id",
                schema: "kasabanka",
                table: "kasa_hareketi",
                column: "islem_id");

            migrationBuilder.CreateIndex(
                name: "ix_kasa_hareketi_kaynak_belge_id",
                schema: "kasabanka",
                table: "kasa_hareketi",
                column: "kaynak_belge_id");

            migrationBuilder.CreateIndex(
                name: "ix_kasa_hesabi_firma_id_kod",
                schema: "kasabanka",
                table: "kasa_hesabi",
                columns: new[] { "firma_id", "kod" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gelir_gider_kaydi",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "kasa_hareketi",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "gelir_gider_kategorisi",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "kasa_hesabi",
                schema: "kasabanka");
        }
    }
}
