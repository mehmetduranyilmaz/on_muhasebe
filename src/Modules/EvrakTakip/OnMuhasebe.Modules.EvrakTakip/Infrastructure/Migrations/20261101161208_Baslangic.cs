using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.EvrakTakip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "evraktakip");

            migrationBuilder.CreateTable(
                name: "degisiklik_kaydi",
                schema: "evraktakip",
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
                name: "takip_evraki",
                schema: "evraktakip",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tur = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    belge_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    kurum = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    baslangic_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    bitis_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    hatirlatma_gun = table.Column<int>(type: "integer", nullable: false),
                    sorumlu_kullanici_id = table.Column<Guid>(type: "uuid", nullable: true),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    silindi_mi = table.Column<bool>(type: "boolean", nullable: false),
                    silinme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_takip_evraki", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "takip_evraki_yenilemesi",
                schema: "evraktakip",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    takip_evraki_id = table.Column<Guid>(type: "uuid", nullable: false),
                    eski_bitis_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    yeni_bitis_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    eski_belge_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    yapan = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_takip_evraki_yenilemesi", x => x.id);
                    table.ForeignKey(
                        name: "fk_takip_evraki_yenilemesi_takip_evraklari_takip_evraki_id",
                        column: x => x.takip_evraki_id,
                        principalSchema: "evraktakip",
                        principalTable: "takip_evraki",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_kayit_id",
                schema: "evraktakip",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_ust_kayit_id",
                schema: "evraktakip",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "ust_kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_takip_evraki_firma_id_bitis_tarihi",
                schema: "evraktakip",
                table: "takip_evraki",
                columns: new[] { "firma_id", "bitis_tarihi" });

            migrationBuilder.CreateIndex(
                name: "ix_takip_evraki_yenilemesi_takip_evraki_id",
                schema: "evraktakip",
                table: "takip_evraki_yenilemesi",
                column: "takip_evraki_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "degisiklik_kaydi",
                schema: "evraktakip");

            migrationBuilder.DropTable(
                name: "takip_evraki_yenilemesi",
                schema: "evraktakip");

            migrationBuilder.DropTable(
                name: "takip_evraki",
                schema: "evraktakip");
        }
    }
}
