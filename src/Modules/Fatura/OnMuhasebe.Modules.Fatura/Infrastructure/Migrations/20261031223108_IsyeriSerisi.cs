using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IsyeriSerisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "isyeri_serisi",
                schema: "fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: false),
                    toptan_seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    perakende_seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    iade_seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    gider_pusulasi_seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    irsaliye_seri = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_isyeri_serisi", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_isyeri_serisi_firma_id_isyeri_id",
                schema: "fatura",
                table: "isyeri_serisi",
                columns: new[] { "firma_id", "isyeri_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "isyeri_serisi",
                schema: "fatura");
        }
    }
}
