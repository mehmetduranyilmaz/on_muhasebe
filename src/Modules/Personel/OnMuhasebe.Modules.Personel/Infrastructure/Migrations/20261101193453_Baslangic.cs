using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Personel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "personel");

            migrationBuilder.CreateTable(
                name: "avans",
                schema: "personel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    taksit_sayisi = table.Column<int>(type: "integer", nullable: false),
                    kalan = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    iptal_mi = table.Column<bool>(type: "boolean", nullable: false),
                    aciklama = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "avans_mahsubu",
                schema: "personel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    avans_id = table.Column<Guid>(type: "uuid", nullable: false),
                    satir_id = table.Column<Guid>(type: "uuid", nullable: false),
                    personel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avans_mahsubu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "degisiklik_kaydi",
                schema: "personel",
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
                name: "maas_donemi",
                schema: "personel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    yil = table.Column<int>(type: "integer", nullable: false),
                    ay = table.Column<int>(type: "integer", nullable: false),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maas_donemi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "masraf_beyani",
                schema: "personel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    beyan_eden_kullanici_id = table.Column<Guid>(type: "uuid", nullable: true),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    kategori = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    masraf_merkezi_id = table.Column<Guid>(type: "uuid", nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    karar_veren_id = table.Column<Guid>(type: "uuid", nullable: true),
                    karar_veren = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    karar_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_masraf_beyani", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personel_karti",
                schema: "personel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    soyad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tc_kimlik = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    ise_giris = table.Column<DateOnly>(type: "date", nullable: false),
                    isten_cikis = table.Column<DateOnly>(type: "date", nullable: true),
                    gorev = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    departman = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    masraf_merkezi_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    net_maas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    eposta = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_personel_karti", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personel_odemesi",
                schema: "personel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    kaynak_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    iptal_mi = table.Column<bool>(type: "boolean", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personel_odemesi", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zimmet",
                schema: "personel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    seri_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verilis_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    iade_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    bedel = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_zimmet", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "maas_satiri",
                schema: "personel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    donem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    personel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    net_maas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    prim = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kesinti = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    avans_kesintisi = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    odenen = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    not = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maas_satiri", x => x.id);
                    table.ForeignKey(
                        name: "fk_maas_satiri_maas_donemi_donem_id",
                        column: x => x.donem_id,
                        principalSchema: "personel",
                        principalTable: "maas_donemi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_avans_firma_id_personel_id",
                schema: "personel",
                table: "avans",
                columns: new[] { "firma_id", "personel_id" });

            migrationBuilder.CreateIndex(
                name: "ix_avans_mahsubu_avans_id",
                schema: "personel",
                table: "avans_mahsubu",
                column: "avans_id");

            migrationBuilder.CreateIndex(
                name: "ix_avans_mahsubu_firma_id_tarih",
                schema: "personel",
                table: "avans_mahsubu",
                columns: new[] { "firma_id", "tarih" });

            migrationBuilder.CreateIndex(
                name: "ix_avans_mahsubu_satir_id",
                schema: "personel",
                table: "avans_mahsubu",
                column: "satir_id");

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_kayit_id",
                schema: "personel",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_ust_kayit_id",
                schema: "personel",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "ust_kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_maas_donemi_firma_id_yil_ay",
                schema: "personel",
                table: "maas_donemi",
                columns: new[] { "firma_id", "yil", "ay" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_maas_satiri_donem_id_personel_id",
                schema: "personel",
                table: "maas_satiri",
                columns: new[] { "donem_id", "personel_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_maas_satiri_personel_id",
                schema: "personel",
                table: "maas_satiri",
                column: "personel_id");

            migrationBuilder.CreateIndex(
                name: "ix_masraf_beyani_firma_id_durum",
                schema: "personel",
                table: "masraf_beyani",
                columns: new[] { "firma_id", "durum" });

            migrationBuilder.CreateIndex(
                name: "ix_masraf_beyani_personel_id",
                schema: "personel",
                table: "masraf_beyani",
                column: "personel_id");

            migrationBuilder.CreateIndex(
                name: "ix_personel_karti_firma_id_kod",
                schema: "personel",
                table: "personel_karti",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personel_karti_firma_id_kullanici_id",
                schema: "personel",
                table: "personel_karti",
                columns: new[] { "firma_id", "kullanici_id" },
                unique: true,
                filter: "kullanici_id IS NOT NULL AND NOT silindi_mi");

            migrationBuilder.CreateIndex(
                name: "ix_personel_karti_firma_id_tc_kimlik",
                schema: "personel",
                table: "personel_karti",
                columns: new[] { "firma_id", "tc_kimlik" },
                unique: true,
                filter: "tc_kimlik IS NOT NULL AND NOT silindi_mi");

            migrationBuilder.CreateIndex(
                name: "ix_personel_odemesi_firma_id_personel_id",
                schema: "personel",
                table: "personel_odemesi",
                columns: new[] { "firma_id", "personel_id" });

            migrationBuilder.CreateIndex(
                name: "ix_personel_odemesi_kaynak_id",
                schema: "personel",
                table: "personel_odemesi",
                column: "kaynak_id");

            migrationBuilder.CreateIndex(
                name: "ix_zimmet_firma_id_personel_id",
                schema: "personel",
                table: "zimmet",
                columns: new[] { "firma_id", "personel_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avans",
                schema: "personel");

            migrationBuilder.DropTable(
                name: "avans_mahsubu",
                schema: "personel");

            migrationBuilder.DropTable(
                name: "degisiklik_kaydi",
                schema: "personel");

            migrationBuilder.DropTable(
                name: "maas_satiri",
                schema: "personel");

            migrationBuilder.DropTable(
                name: "masraf_beyani",
                schema: "personel");

            migrationBuilder.DropTable(
                name: "personel_karti",
                schema: "personel");

            migrationBuilder.DropTable(
                name: "personel_odemesi",
                schema: "personel");

            migrationBuilder.DropTable(
                name: "zimmet",
                schema: "personel");

            migrationBuilder.DropTable(
                name: "maas_donemi",
                schema: "personel");
        }
    }
}
