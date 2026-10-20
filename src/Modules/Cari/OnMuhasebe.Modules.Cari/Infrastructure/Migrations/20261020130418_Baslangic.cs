using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Cari.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cari");

            migrationBuilder.CreateTable(
                name: "cari_grubu",
                schema: "cari",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cari_grubu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cari_hesap",
                schema: "cari",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unvan = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    tur = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kisi_tipi = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vergi_no = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    vergi_dairesi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    il = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ilce = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    adres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    eposta = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    vade_gun = table.Column<int>(type: "integer", nullable: true),
                    risk_limiti = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    grup_id = table.Column<Guid>(type: "uuid", nullable: true),
                    yetkili_ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    yetkili_telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    not = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_cari_hesap", x => x.id);
                    table.ForeignKey(
                        name: "fk_cari_hesap_cari_grubu_grup_id",
                        column: x => x.grup_id,
                        principalSchema: "cari",
                        principalTable: "cari_grubu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cari_hareket",
                schema: "cari",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    vade_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    tur = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    belge_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    borc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    alacak = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kur = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    borc_tl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    alacak_tl = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kaynak_belge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iptal_edilen_hareket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iptal_hareket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cari_hareket", x => x.id);
                    table.ForeignKey(
                        name: "fk_cari_hareket_cari_hesaplar_cari_hesap_id",
                        column: x => x.cari_hesap_id,
                        principalSchema: "cari",
                        principalTable: "cari_hesap",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cari_grubu_firma_id_ad",
                schema: "cari",
                table: "cari_grubu",
                columns: new[] { "firma_id", "ad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cari_hareket_cari_hesap_id",
                schema: "cari",
                table: "cari_hareket",
                column: "cari_hesap_id");

            migrationBuilder.CreateIndex(
                name: "ix_cari_hareket_firma_id_cari_hesap_id_tarih",
                schema: "cari",
                table: "cari_hareket",
                columns: new[] { "firma_id", "cari_hesap_id", "tarih" })
                .Annotation("Npgsql:IndexInclude", new[] { "borc", "alacak", "borc_tl", "alacak_tl" });

            migrationBuilder.CreateIndex(
                name: "ix_cari_hareket_kaynak_belge_id",
                schema: "cari",
                table: "cari_hareket",
                column: "kaynak_belge_id");

            migrationBuilder.CreateIndex(
                name: "ix_cari_hesap_firma_id_kod",
                schema: "cari",
                table: "cari_hesap",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cari_hesap_firma_id_unvan",
                schema: "cari",
                table: "cari_hesap",
                columns: new[] { "firma_id", "unvan" });

            migrationBuilder.CreateIndex(
                name: "ix_cari_hesap_grup_id",
                schema: "cari",
                table: "cari_hesap",
                column: "grup_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cari_hareket",
                schema: "cari");

            migrationBuilder.DropTable(
                name: "cari_hesap",
                schema: "cari");

            migrationBuilder.DropTable(
                name: "cari_grubu",
                schema: "cari");
        }
    }
}
