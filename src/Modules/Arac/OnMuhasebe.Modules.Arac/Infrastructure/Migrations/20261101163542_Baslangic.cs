using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Arac.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "arac");

            migrationBuilder.CreateTable(
                name: "arac_karti",
                schema: "arac",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaka = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    marka = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_yili = table.Column<int>(type: "integer", nullable: true),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    yakit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sasi_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    motor_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ruhsat_sahibi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    mulkiyet = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    surucu = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    surucu_telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    guncel_km = table.Column<int>(type: "integer", nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    demirbas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    masraf_merkezi_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_arac_karti", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "degisiklik_kaydi",
                schema: "arac",
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
                name: "arac_hasari",
                schema: "arac",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arac_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    kusur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    onarim_tutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sigorta_odemesi = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    dosya_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arac_hasari", x => x.id);
                    table.ForeignKey(
                        name: "fk_arac_hasari_araclar_arac_id",
                        column: x => x.arac_id,
                        principalSchema: "arac",
                        principalTable: "arac_karti",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arac_masrafi",
                schema: "arac",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arac_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    km = table.Column<int>(type: "integer", nullable: true),
                    litre = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    tedarikci = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    belge_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fatura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arac_masrafi", x => x.id);
                    table.ForeignKey(
                        name: "fk_arac_masrafi_arac_karti_arac_id",
                        column: x => x.arac_id,
                        principalSchema: "arac",
                        principalTable: "arac_karti",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arac_mtv",
                schema: "arac",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arac_id = table.Column<Guid>(type: "uuid", nullable: false),
                    yil = table.Column<int>(type: "integer", nullable: false),
                    taksit = table.Column<int>(type: "integer", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    son_odeme_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    odeme_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arac_mtv", x => x.id);
                    table.ForeignKey(
                        name: "fk_arac_mtv_arac_karti_arac_id",
                        column: x => x.arac_id,
                        principalSchema: "arac",
                        principalTable: "arac_karti",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arac_yukumlulugu",
                schema: "arac",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arac_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    baslangic_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    bitis_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    belge_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sirket = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    hatirlatma_gun = table.Column<int>(type: "integer", nullable: false),
                    yenilendi_mi = table.Column<bool>(type: "boolean", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arac_yukumlulugu", x => x.id);
                    table.ForeignKey(
                        name: "fk_arac_yukumlulugu_arac_karti_arac_id",
                        column: x => x.arac_id,
                        principalSchema: "arac",
                        principalTable: "arac_karti",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arac_hasari_arac_id_tarih",
                schema: "arac",
                table: "arac_hasari",
                columns: new[] { "arac_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_arac_karti_firma_id_plaka",
                schema: "arac",
                table: "arac_karti",
                columns: new[] { "firma_id", "plaka" },
                unique: true,
                filter: "silindi_mi = false");

            migrationBuilder.CreateIndex(
                name: "ix_arac_masrafi_arac_id_tarih",
                schema: "arac",
                table: "arac_masrafi",
                columns: new[] { "arac_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_arac_mtv_arac_id_yil_taksit",
                schema: "arac",
                table: "arac_mtv",
                columns: new[] { "arac_id", "yil", "taksit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_arac_yukumlulugu_arac_id",
                schema: "arac",
                table: "arac_yukumlulugu",
                column: "arac_id");

            migrationBuilder.CreateIndex(
                name: "ix_arac_yukumlulugu_firma_id_bitis_tarihi",
                schema: "arac",
                table: "arac_yukumlulugu",
                columns: new[] { "firma_id", "bitis_tarihi" });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_kayit_id",
                schema: "arac",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_ust_kayit_id",
                schema: "arac",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "ust_kayit_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arac_hasari",
                schema: "arac");

            migrationBuilder.DropTable(
                name: "arac_masrafi",
                schema: "arac");

            migrationBuilder.DropTable(
                name: "arac_mtv",
                schema: "arac");

            migrationBuilder.DropTable(
                name: "arac_yukumlulugu",
                schema: "arac");

            migrationBuilder.DropTable(
                name: "degisiklik_kaydi",
                schema: "arac");

            migrationBuilder.DropTable(
                name: "arac_karti",
                schema: "arac");
        }
    }
}
