using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Bildirim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bildirim");

            migrationBuilder.CreateTable(
                name: "bildirim",
                schema: "bildirim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    anahtar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    baslik = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    metin = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    baglanti = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    onem = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    okunma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eposta = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bildirim", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bildirim_ayari",
                schema: "bildirim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vade_oncesi_gun = table.Column<int>(type: "integer", nullable: false),
                    kapali_turler = table.Column<List<string>>(type: "text[]", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bildirim_ayari", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "degisiklik_kaydi",
                schema: "bildirim",
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
                name: "eposta_ayari",
                schema: "bildirim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    sunucu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: false),
                    guvenlik = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    kullanici_adi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sifre_sifreli = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    gonderen_adres = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    gonderen_ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eposta_ayari", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kullanici_tercihi",
                schema: "bildirim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bildirim_alir = table.Column<bool>(type: "boolean", nullable: false),
                    kapali_turler = table.Column<List<string>>(type: "text[]", nullable: false),
                    eposta = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    son_ozet_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kullanici_tercihi", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bildirim_eposta",
                schema: "bildirim",
                table: "bildirim",
                column: "eposta",
                filter: "eposta = 'Bekliyor'");

            migrationBuilder.CreateIndex(
                name: "ix_bildirim_firma_id_kullanici_id_anahtar",
                schema: "bildirim",
                table: "bildirim",
                columns: new[] { "firma_id", "kullanici_id", "anahtar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bildirim_firma_id_kullanici_id_okunma_tarihi",
                schema: "bildirim",
                table: "bildirim",
                columns: new[] { "firma_id", "kullanici_id", "okunma_tarihi" });

            migrationBuilder.CreateIndex(
                name: "ix_bildirim_ayari_firma_id",
                schema: "bildirim",
                table: "bildirim_ayari",
                column: "firma_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_kayit_id",
                schema: "bildirim",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_ust_kayit_id",
                schema: "bildirim",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "ust_kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_kullanici_tercihi_firma_id_kullanici_id",
                schema: "bildirim",
                table: "kullanici_tercihi",
                columns: new[] { "firma_id", "kullanici_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bildirim",
                schema: "bildirim");

            migrationBuilder.DropTable(
                name: "bildirim_ayari",
                schema: "bildirim");

            migrationBuilder.DropTable(
                name: "degisiklik_kaydi",
                schema: "bildirim");

            migrationBuilder.DropTable(
                name: "eposta_ayari",
                schema: "bildirim");

            migrationBuilder.DropTable(
                name: "kullanici_tercihi",
                schema: "bildirim");
        }
    }
}
