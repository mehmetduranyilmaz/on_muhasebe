using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Demirbas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demirbas",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sicil_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    grup = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    binek_otomobil_mi = table.Column<bool>(type: "boolean", nullable: false),
                    edinme_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    edinme_bedeli = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    faydali_omur = table.Column<int>(type: "integer", nullable: false),
                    yontem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    belge_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    zimmetli = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    konum = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    masraf_merkezi_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    el_cikarma_turu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    el_cikarma_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    el_cikarma_bedeli = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    el_cikarma_aciklamasi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_demirbas", x => x.id);
                    table.ForeignKey(
                        name: "fk_demirbas_masraf_merkezleri_masraf_merkezi_id",
                        column: x => x.masraf_merkezi_id,
                        principalSchema: "kasabanka",
                        principalTable: "masraf_merkezi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_demirbas_firma_id_sicil_no",
                schema: "kasabanka",
                table: "demirbas",
                columns: new[] { "firma_id", "sicil_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_demirbas_masraf_merkezi_id",
                schema: "kasabanka",
                table: "demirbas",
                column: "masraf_merkezi_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demirbas",
                schema: "kasabanka");
        }
    }
}
