using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fatura");

            migrationBuilder.CreateTable(
                name: "fatura",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    satis_sekli = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    durum = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    numara = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    vade_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cari_kodu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cari_unvan = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    vergi_no = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    vergi_dairesi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    il = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ilce = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    adres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    kur = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    genel_iskonto_turu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    genel_iskonto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    brut_toplam = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    satir_iskonto_toplami = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    genel_iskonto_tutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    matrah = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kdv_toplami = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    genel_toplam = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    genel_toplam_tl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ettn = table.Column<Guid>(type: "uuid", nullable: false),
                    onay_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    iptal_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_fatura", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fatura_ayari",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kayit_akisi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    toptan_seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    perakende_seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    iade_seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    toptan_kdv_dahil = table.Column<bool>(type: "boolean", nullable: false),
                    perakende_kdv_dahil = table.Column<bool>(type: "boolean", nullable: false),
                    varsayilan_vade_gunu = table.Column<int>(type: "integer", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fatura_ayari", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fatura_seri_sayaci",
                schema: "fatura",
                columns: table => new
                {
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    yil = table.Column<int>(type: "integer", nullable: false),
                    son_numara = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fatura_seri_sayaci", x => new { x.firma_id, x.seri, x.yil });
                });

            migrationBuilder.CreateTable(
                name: "fatura_satiri",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fatura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    stok_karti_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stok_kodu = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ad = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    stoklu_mu = table.Column<bool>(type: "boolean", nullable: false),
                    depo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    birim = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    birim_fiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    kdv_dahil_mi = table.Column<bool>(type: "boolean", nullable: false),
                    iskonto1 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    iskonto2 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    iskonto3 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    kdv_orani = table.Column<int>(type: "integer", nullable: false),
                    brut_tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    iskonto_tutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    genel_iskonto_payi = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    matrah = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kdv_tutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    toplam = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fatura_satiri", x => x.id);
                    table.ForeignKey(
                        name: "fk_fatura_satiri_fatura_fatura_id",
                        column: x => x.fatura_id,
                        principalSchema: "fatura",
                        principalTable: "fatura",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fatura_firma_id_cari_hesap_id",
                schema: "fatura",
                table: "fatura",
                columns: new[] { "firma_id", "cari_hesap_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fatura_firma_id_numara",
                schema: "fatura",
                table: "fatura",
                columns: new[] { "firma_id", "numara" },
                unique: true,
                filter: "numara IS NOT NULL AND tur IN ('Satis', 'AlistanIade')");

            migrationBuilder.CreateIndex(
                name: "ix_fatura_firma_id_tarih",
                schema: "fatura",
                table: "fatura",
                columns: new[] { "firma_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_fatura_ayari_firma_id",
                schema: "fatura",
                table: "fatura_ayari",
                column: "firma_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fatura_satiri_fatura_id",
                schema: "fatura",
                table: "fatura_satiri",
                column: "fatura_id");

            migrationBuilder.CreateIndex(
                name: "ix_fatura_satiri_stok_karti_id",
                schema: "fatura",
                table: "fatura_satiri",
                column: "stok_karti_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fatura_ayari",
                schema: "fatura");

            migrationBuilder.DropTable(
                name: "fatura_satiri",
                schema: "fatura");

            migrationBuilder.DropTable(
                name: "fatura_seri_sayaci",
                schema: "fatura");

            migrationBuilder.DropTable(
                name: "fatura",
                schema: "fatura");
        }
    }
}
