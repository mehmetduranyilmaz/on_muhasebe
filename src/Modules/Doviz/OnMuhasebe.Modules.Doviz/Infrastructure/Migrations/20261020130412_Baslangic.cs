using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Doviz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "doviz");

            migrationBuilder.CreateTable(
                name: "kur",
                schema: "doviz",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    doviz_alis = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    doviz_satis = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    efektif_alis = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    efektif_satis = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kur", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kur_bulteni",
                schema: "doviz",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    yayimlandi_mi = table.Column<bool>(type: "boolean", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kur_bulteni", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kur_tarih_para_birimi",
                schema: "doviz",
                table: "kur",
                columns: new[] { "tarih", "para_birimi" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kur_bulteni_tarih",
                schema: "doviz",
                table: "kur_bulteni",
                column: "tarih",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kur",
                schema: "doviz");

            migrationBuilder.DropTable(
                name: "kur_bulteni",
                schema: "doviz");
        }
    }
}
