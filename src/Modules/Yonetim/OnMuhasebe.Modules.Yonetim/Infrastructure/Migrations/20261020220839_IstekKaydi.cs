using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IstekKaydi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "istek_kaydi",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anahtar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ozet = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tamamlandi = table.Column<bool>(type: "boolean", nullable: false),
                    durum_kodu = table.Column<int>(type: "integer", nullable: true),
                    icerik_turu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    konum = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    govde = table.Column<byte[]>(type: "bytea", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_istek_kaydi", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_istek_kaydi_firma_id_kullanici_id_anahtar",
                schema: "yonetim",
                table: "istek_kaydi",
                columns: new[] { "firma_id", "kullanici_id", "anahtar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_istek_kaydi_olusturma_tarihi",
                schema: "yonetim",
                table: "istek_kaydi",
                column: "olusturma_tarihi");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "istek_kaydi",
                schema: "yonetim");
        }
    }
}
