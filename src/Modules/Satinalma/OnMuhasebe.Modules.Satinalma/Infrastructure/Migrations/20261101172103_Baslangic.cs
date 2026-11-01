using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Satinalma.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "satinalma");

            migrationBuilder.CreateTable(
                name: "degisiklik_kaydi",
                schema: "satinalma",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kullanici_adi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    zaman = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tablo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kayit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ust_kayit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    islem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    alanlar = table.Column<string>(type: "jsonb", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_degisiklik_kaydi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "onay_basamagi",
                schema: "satinalma",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    en_az_tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ad = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_onay_basamagi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "satinalma_talebi",
                schema: "satinalma",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    talep_eden_id = table.Column<Guid>(type: "uuid", nullable: false),
                    talep_eden = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    gerekli_tarih = table.Column<DateOnly>(type: "date", nullable: true),
                    gerekce = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    masraf_merkezi_id = table.Column<Guid>(type: "uuid", nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    toplam = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gonderim_no = table.Column<int>(type: "integer", nullable: false),
                    siparis_id = table.Column<Guid>(type: "uuid", nullable: true),
                    karar_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_satinalma_talebi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "talep_kalemi",
                schema: "satinalma",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    talep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stok_karti_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ad = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    birim = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tahmini_birim_fiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    kdv_orani = table.Column<int>(type: "integer", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_talep_kalemi", x => x.id);
                    table.ForeignKey(
                        name: "fk_talep_kalemi_satinalma_talebi_talep_id",
                        column: x => x.talep_id,
                        principalSchema: "satinalma",
                        principalTable: "satinalma_talebi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "talep_onayi",
                schema: "satinalma",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    talep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gonderim_no = table.Column<int>(type: "integer", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: true),
                    onaylayan = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    karar_veren_id = table.Column<Guid>(type: "uuid", nullable: true),
                    karar_veren = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    karar_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_talep_onayi", x => x.id);
                    table.ForeignKey(
                        name: "fk_talep_onayi_satinalma_talebi_talep_id",
                        column: x => x.talep_id,
                        principalSchema: "satinalma",
                        principalTable: "satinalma_talebi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_kayit_id",
                schema: "satinalma",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_ust_kayit_id",
                schema: "satinalma",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "ust_kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_onay_basamagi_firma_id_sira",
                schema: "satinalma",
                table: "onay_basamagi",
                columns: new[] { "firma_id", "sira" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_satinalma_talebi_firma_id_durum",
                schema: "satinalma",
                table: "satinalma_talebi",
                columns: new[] { "firma_id", "durum" });

            migrationBuilder.CreateIndex(
                name: "ix_satinalma_talebi_firma_id_kod",
                schema: "satinalma",
                table: "satinalma_talebi",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_talep_kalemi_talep_id",
                schema: "satinalma",
                table: "talep_kalemi",
                column: "talep_id");

            migrationBuilder.CreateIndex(
                name: "ix_talep_onayi_talep_id_gonderim_no_sira",
                schema: "satinalma",
                table: "talep_onayi",
                columns: new[] { "talep_id", "gonderim_no", "sira" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "degisiklik_kaydi",
                schema: "satinalma");

            migrationBuilder.DropTable(
                name: "onay_basamagi",
                schema: "satinalma");

            migrationBuilder.DropTable(
                name: "talep_kalemi",
                schema: "satinalma");

            migrationBuilder.DropTable(
                name: "talep_onayi",
                schema: "satinalma");

            migrationBuilder.DropTable(
                name: "satinalma_talebi",
                schema: "satinalma");
        }
    }
}
