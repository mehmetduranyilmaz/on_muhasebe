using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Muhasebe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "muhasebe");

            migrationBuilder.CreateTable(
                name: "degisiklik_kaydi",
                schema: "muhasebe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kullanici_adi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    zaman = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tablo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kayit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ust_kayit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    islem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    alanlar = table.Column<string>(type: "jsonb", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_degisiklik_kaydi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hesap_kodu",
                schema: "muhasebe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hesap_kodu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kod_eslemesi",
                schema: "muhasebe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kart_turu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kod_eslemesi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "muhasebe_ayari",
                schema: "muhasebe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ayirac = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    cari_alt_grup = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_muhasebe_ayari", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_kayit_id",
                schema: "muhasebe",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_ust_kayit_id",
                schema: "muhasebe",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "ust_kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_hesap_kodu_firma_id_rol",
                schema: "muhasebe",
                table: "hesap_kodu",
                columns: new[] { "firma_id", "rol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kod_eslemesi_firma_id_kart_turu_kart_id",
                schema: "muhasebe",
                table: "kod_eslemesi",
                columns: new[] { "firma_id", "kart_turu", "kart_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_muhasebe_ayari_firma_id",
                schema: "muhasebe",
                table: "muhasebe_ayari",
                column: "firma_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "degisiklik_kaydi",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "hesap_kodu",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "kod_eslemesi",
                schema: "muhasebe");

            migrationBuilder.DropTable(
                name: "muhasebe_ayari",
                schema: "muhasebe");
        }
    }
}
