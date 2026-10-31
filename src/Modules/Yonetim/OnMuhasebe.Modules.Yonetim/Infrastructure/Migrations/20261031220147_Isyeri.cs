using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Isyeri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "isyeri_limiti",
                schema: "yonetim",
                table: "paket",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "isyerleri",
                schema: "yonetim",
                table: "firma_kullanicisi",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<Guid>(
                name: "varsayilan_isyeri_id",
                schema: "yonetim",
                table: "firma_kullanicisi",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "isyeri",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    adres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    il = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ilce = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sgk_sicil_no = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    merkez_mi = table.Column<bool>(type: "boolean", nullable: false),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_isyeri", x => x.id);
                    table.ForeignKey(
                        name: "fk_isyeri_firma_firma_id",
                        column: x => x.firma_id,
                        principalSchema: "yonetim",
                        principalTable: "firma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_isyeri_firma_id",
                schema: "yonetim",
                table: "isyeri",
                column: "firma_id",
                unique: true,
                filter: "merkez_mi");

            migrationBuilder.CreateIndex(
                name: "ix_isyeri_firma_id_kod",
                schema: "yonetim",
                table: "isyeri",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            // Her firmaya merkez işyeri açılır; mevcut belge, depo ve kasalar buna bağlanır.
            migrationBuilder.Sql("""
                INSERT INTO yonetim.isyeri (id, firma_id, kod, ad, merkez_mi, aktif_mi, olusturma_tarihi)
                SELECT gen_random_uuid(), f.id, 'MRK', 'Merkez', true, true, now() FROM yonetim.firma f;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "isyeri",
                schema: "yonetim");

            migrationBuilder.DropColumn(
                name: "isyeri_limiti",
                schema: "yonetim",
                table: "paket");

            migrationBuilder.DropColumn(
                name: "isyerleri",
                schema: "yonetim",
                table: "firma_kullanicisi");

            migrationBuilder.DropColumn(
                name: "varsayilan_isyeri_id",
                schema: "yonetim",
                table: "firma_kullanicisi");
        }
    }
}
