using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Muhasebe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MuhasebeAktarimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "muhasebe_aktarimi",
                schema: "muhasebe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    baslangic = table.Column<DateOnly>(type: "date", nullable: false),
                    bitis = table.Column<DateOnly>(type: "date", nullable: false),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fis_sayisi = table.Column<int>(type: "integer", nullable: false),
                    toplam_borc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aktaran_adi = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("pk_muhasebe_aktarimi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "aktarilan_fis",
                schema: "muhasebe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aktarim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kaynak = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kaynak_id = table.Column<Guid>(type: "uuid", nullable: false),
                    belge_turu = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    belge_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ozet = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aktarilan_fis", x => x.id);
                    table.ForeignKey(
                        name: "fk_aktarilan_fis_aktarimlar_aktarim_id",
                        column: x => x.aktarim_id,
                        principalSchema: "muhasebe",
                        principalTable: "muhasebe_aktarimi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aktarilan_fis_aktarim_id",
                schema: "muhasebe",
                table: "aktarilan_fis",
                column: "aktarim_id");

            migrationBuilder.CreateIndex(
                name: "ix_aktarilan_fis_firma_id_kaynak_kaynak_id",
                schema: "muhasebe",
                table: "aktarilan_fis",
                columns: new[] { "firma_id", "kaynak", "kaynak_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_aktarilan_fis_firma_id_tarih",
                schema: "muhasebe",
                table: "aktarilan_fis",
                columns: new[] { "firma_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_muhasebe_aktarimi_firma_id_baslangic",
                schema: "muhasebe",
                table: "muhasebe_aktarimi",
                columns: new[] { "firma_id", "baslangic" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aktarilan_fis",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "muhasebe_aktarimi",
                schema: "muhasebe");
        }
    }
}
