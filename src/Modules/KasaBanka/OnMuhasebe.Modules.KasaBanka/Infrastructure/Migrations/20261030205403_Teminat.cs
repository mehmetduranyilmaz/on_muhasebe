using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Teminat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teminat",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    yon = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taraf_adi = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    referans_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    verilis_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    vade_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    iade_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    durum = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teminat", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_teminat_firma_id_durum",
                schema: "kasabanka",
                table: "teminat",
                columns: new[] { "firma_id", "durum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teminat",
                schema: "kasabanka");
        }
    }
}
