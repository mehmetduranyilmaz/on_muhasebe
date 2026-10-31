using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NakdiKredi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kredi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    durum = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    kullandirim_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    aylik_faiz_orani = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    kullandirim_masrafi = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    kullandirim_islem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kredi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kredi_taksiti",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kredi_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    vade = table.Column<DateOnly>(type: "date", nullable: false),
                    anapara = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    faiz = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vergi = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    odeme_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    odeme_islem_id = table.Column<Guid>(type: "uuid", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kredi_taksiti", x => x.id);
                    table.ForeignKey(
                        name: "fk_kredi_taksiti_kredi_kredi_id",
                        column: x => x.kredi_id,
                        principalSchema: "kasabanka",
                        principalTable: "kredi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kredi_firma_id_kod",
                schema: "kasabanka",
                table: "kredi",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kredi_taksiti_kredi_id_sira",
                schema: "kasabanka",
                table: "kredi_taksiti",
                columns: new[] { "kredi_id", "sira" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kredi_taksiti",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "kredi",
                schema: "kasabanka");
        }
    }
}
