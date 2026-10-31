using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TeklifSiparis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teklif_siparis",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    durum = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    numara = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    gecerlilik_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    termin_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
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
                    odeme_kosulu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    teslim_kosulu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    brut_toplam = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    satir_iskonto_toplami = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    genel_iskonto_tutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    matrah = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kdv_toplami = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    genel_toplam = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kaynak_teklif_id = table.Column<Guid>(type: "uuid", nullable: true),
                    onay_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_teklif_siparis", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teklif_siparis_satiri",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    teklif_siparis_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    kdv_orani = table.Column<int>(type: "integer", nullable: false),
                    iskonto1 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    iskonto2 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    iskonto3 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    matrah = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kdv_tutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    toplam = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    karsilanan_miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teklif_siparis_satiri", x => x.id);
                    table.ForeignKey(
                        name: "fk_teklif_siparis_satiri_teklif_siparis_teklif_siparis_id",
                        column: x => x.teklif_siparis_id,
                        principalSchema: "fatura",
                        principalTable: "teklif_siparis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_teklif_siparis_firma_id_cari_hesap_id",
                schema: "fatura",
                table: "teklif_siparis",
                columns: new[] { "firma_id", "cari_hesap_id" });

            migrationBuilder.CreateIndex(
                name: "ix_teklif_siparis_firma_id_numara",
                schema: "fatura",
                table: "teklif_siparis",
                columns: new[] { "firma_id", "numara" },
                unique: true,
                filter: "numara IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_teklif_siparis_firma_id_tarih",
                schema: "fatura",
                table: "teklif_siparis",
                columns: new[] { "firma_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_teklif_siparis_kaynak_teklif_id",
                schema: "fatura",
                table: "teklif_siparis",
                column: "kaynak_teklif_id");

            migrationBuilder.CreateIndex(
                name: "ix_teklif_siparis_satiri_stok_karti_id",
                schema: "fatura",
                table: "teklif_siparis_satiri",
                column: "stok_karti_id");

            migrationBuilder.CreateIndex(
                name: "ix_teklif_siparis_satiri_teklif_siparis_id",
                schema: "fatura",
                table: "teklif_siparis_satiri",
                column: "teklif_siparis_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teklif_siparis_satiri",
                schema: "fatura");

            migrationBuilder.DropTable(
                name: "teklif_siparis",
                schema: "fatura");
        }
    }
}
