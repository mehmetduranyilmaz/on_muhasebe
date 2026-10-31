using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Irsaliye : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "irsaliye_satiri_id",
                schema: "fatura",
                table: "fatura_satiri",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "irsaliye_seri",
                schema: "fatura",
                table: "fatura_ayari",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "IRS");

            migrationBuilder.CreateTable(
                name: "irsaliye",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    durum = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    numara = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    sevk_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    sevk_saati = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
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
                    hedef_depo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    teslim_adresi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    arac_plakasi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sofor_adi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sofor_tckn = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    tasiyici_unvani = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    tasiyici_vkn = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_irsaliye", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "irsaliye_satiri",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    irsaliye_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    stok_karti_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stok_kodu = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ad = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    depo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    birim = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    birim_fiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    kdv_orani = table.Column<int>(type: "integer", nullable: false),
                    faturalanan_miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_irsaliye_satiri", x => x.id);
                    table.ForeignKey(
                        name: "fk_irsaliye_satiri_irsaliye_irsaliye_id",
                        column: x => x.irsaliye_id,
                        principalSchema: "fatura",
                        principalTable: "irsaliye",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fatura_satiri_irsaliye_satiri_id",
                schema: "fatura",
                table: "fatura_satiri",
                column: "irsaliye_satiri_id");

            migrationBuilder.CreateIndex(
                name: "ix_irsaliye_firma_id_cari_hesap_id",
                schema: "fatura",
                table: "irsaliye",
                columns: new[] { "firma_id", "cari_hesap_id" });

            migrationBuilder.CreateIndex(
                name: "ix_irsaliye_firma_id_numara",
                schema: "fatura",
                table: "irsaliye",
                columns: new[] { "firma_id", "numara" },
                unique: true,
                filter: "numara IS NOT NULL AND tur IN ('Satis', 'AlistanIade', 'DepoSevk')");

            migrationBuilder.CreateIndex(
                name: "ix_irsaliye_firma_id_tarih",
                schema: "fatura",
                table: "irsaliye",
                columns: new[] { "firma_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_irsaliye_satiri_irsaliye_id",
                schema: "fatura",
                table: "irsaliye_satiri",
                column: "irsaliye_id");

            migrationBuilder.CreateIndex(
                name: "ix_irsaliye_satiri_stok_karti_id",
                schema: "fatura",
                table: "irsaliye_satiri",
                column: "stok_karti_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "irsaliye_satiri",
                schema: "fatura");

            migrationBuilder.DropTable(
                name: "irsaliye",
                schema: "fatura");

            migrationBuilder.DropIndex(
                name: "ix_fatura_satiri_irsaliye_satiri_id",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "irsaliye_satiri_id",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "irsaliye_seri",
                schema: "fatura",
                table: "fatura_ayari");
        }
    }
}
