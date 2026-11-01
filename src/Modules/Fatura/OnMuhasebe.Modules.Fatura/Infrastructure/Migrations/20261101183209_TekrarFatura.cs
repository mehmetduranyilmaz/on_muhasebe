using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TekrarFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tekrar_plani",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    siklik = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gun = table.Column<int>(type: "integer", nullable: false),
                    baslangic = table.Column<DateOnly>(type: "date", nullable: false),
                    bitis = table.Column<DateOnly>(type: "date", nullable: true),
                    vade_gun = table.Column<int>(type: "integer", nullable: true),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    otomatik_kesinlestir = table.Column<bool>(type: "boolean", nullable: false),
                    stopaj_orani = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    masraf_merkezi_id = table.Column<Guid>(type: "uuid", nullable: true),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    artis_orani = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    artis_ayi = table.Column<int>(type: "integer", nullable: true),
                    son_artis_yili = table.Column<int>(type: "integer", nullable: false),
                    sonraki_tarih = table.Column<DateOnly>(type: "date", nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    son_hata = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    son_hata_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    satirlar = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_tekrar_plani", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tekrar_uretimi",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    donem = table.Column<DateOnly>(type: "date", nullable: false),
                    fatura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uygulanan_artis = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tekrar_uretimi", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tekrar_plani_firma_id_durum_sonraki_tarih",
                schema: "fatura",
                table: "tekrar_plani",
                columns: new[] { "firma_id", "durum", "sonraki_tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_tekrar_plani_firma_id_kod",
                schema: "fatura",
                table: "tekrar_plani",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tekrar_uretimi_fatura_id",
                schema: "fatura",
                table: "tekrar_uretimi",
                column: "fatura_id");

            migrationBuilder.CreateIndex(
                name: "ix_tekrar_uretimi_plan_id_donem",
                schema: "fatura",
                table: "tekrar_uretimi",
                columns: new[] { "plan_id", "donem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tekrar_plani",
                schema: "fatura");

            migrationBuilder.DropTable(
                name: "tekrar_uretimi",
                schema: "fatura");
        }
    }
}
