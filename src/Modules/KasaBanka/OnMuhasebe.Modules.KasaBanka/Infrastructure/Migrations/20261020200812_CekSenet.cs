using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CekSenet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cek_senet_ayari",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kesideci_kimlik_zorunlu = table.Column<bool>(type: "boolean", nullable: false),
                    senet_borclu_kimlik_zorunlu = table.Column<bool>(type: "boolean", nullable: false),
                    risk_davranisi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cek_senet_ayari", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evrak_borclusu",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kimlik_no = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    adres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    not = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    kara_listede = table.Column<bool>(type: "boolean", nullable: false),
                    uyari_kapali = table.Column<bool>(type: "boolean", nullable: false),
                    risk_notu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evrak_borclusu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evrak_bordrosu",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    islem = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_evrak_bordrosu", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "borclu_hesabi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    borclu_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banka_adi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sube = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hesap_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_borclu_hesabi", x => x.id);
                    table.ForeignKey(
                        name: "fk_borclu_hesabi_evrak_borclusu_borclu_id",
                        column: x => x.borclu_id,
                        principalSchema: "kasabanka",
                        principalTable: "evrak_borclusu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evrak",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tur = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    yon = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    seri_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    giris_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    vade_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    kur = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cari_tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cari_kur = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: false),
                    borclu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    borclu_adi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    borclu_kimlik_no = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    banka_adi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sube = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hesap_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    keside_yeri = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    kefil_adi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    kefil_kimlik_no = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    kendi_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    konum_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ciro_cari_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ciro_cari_tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ciro_cari_kur = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    tahsil_edilen = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    karsiliksiz_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    risk_onaylayan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    risk_onay_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_evrak", x => x.id);
                    table.ForeignKey(
                        name: "fk_evrak_evrak_borclusu_borclu_id",
                        column: x => x.borclu_id,
                        principalSchema: "kasabanka",
                        principalTable: "evrak_borclusu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evrak_hesaplar_kendi_hesap_id",
                        column: x => x.kendi_hesap_id,
                        principalSchema: "kasabanka",
                        principalTable: "kasa_hesabi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evrak_hesaplar_konum_hesap_id",
                        column: x => x.konum_hesap_id,
                        principalSchema: "kasabanka",
                        principalTable: "kasa_hesabi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evrak_hareketi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evrak_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bordro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    islem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    islem = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    tarih = table.Column<DateOnly>(type: "date", nullable: false),
                    onceki_durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    yeni_durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    geri_alindi = table.Column<bool>(type: "boolean", nullable: false),
                    geri_alma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    onceki_konum_hesap_id = table.Column<Guid>(type: "uuid", nullable: true),
                    onceki_ciro_cari_id = table.Column<Guid>(type: "uuid", nullable: true),
                    onceki_ciro_cari_tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    onceki_ciro_cari_kur = table.Column<decimal>(type: "numeric(19,6)", precision: 19, scale: 6, nullable: true),
                    onceki_karsiliksiz_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evrak_hareketi", x => x.id);
                    table.ForeignKey(
                        name: "fk_evrak_hareketi_evrak_bordrosu_bordro_id",
                        column: x => x.bordro_id,
                        principalSchema: "kasabanka",
                        principalTable: "evrak_bordrosu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evrak_hareketi_evrak_evrak_id",
                        column: x => x.evrak_id,
                        principalSchema: "kasabanka",
                        principalTable: "evrak",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_borclu_hesabi_borclu_id",
                schema: "kasabanka",
                table: "borclu_hesabi",
                column: "borclu_id");

            migrationBuilder.CreateIndex(
                name: "ix_cek_senet_ayari_firma_id",
                schema: "kasabanka",
                table: "cek_senet_ayari",
                column: "firma_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evrak_borclu_id",
                schema: "kasabanka",
                table: "evrak",
                column: "borclu_id");

            migrationBuilder.CreateIndex(
                name: "ix_evrak_firma_id_borclu_id",
                schema: "kasabanka",
                table: "evrak",
                columns: new[] { "firma_id", "borclu_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evrak_firma_id_cari_hesap_id",
                schema: "kasabanka",
                table: "evrak",
                columns: new[] { "firma_id", "cari_hesap_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evrak_firma_id_kod",
                schema: "kasabanka",
                table: "evrak",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evrak_firma_id_yon_durum_vade_tarihi",
                schema: "kasabanka",
                table: "evrak",
                columns: new[] { "firma_id", "yon", "durum", "vade_tarihi" });

            migrationBuilder.CreateIndex(
                name: "ix_evrak_kendi_hesap_id",
                schema: "kasabanka",
                table: "evrak",
                column: "kendi_hesap_id");

            migrationBuilder.CreateIndex(
                name: "ix_evrak_konum_hesap_id",
                schema: "kasabanka",
                table: "evrak",
                column: "konum_hesap_id");

            migrationBuilder.CreateIndex(
                name: "ix_evrak_borclusu_firma_id_ad",
                schema: "kasabanka",
                table: "evrak_borclusu",
                columns: new[] { "firma_id", "ad" });

            migrationBuilder.CreateIndex(
                name: "ix_evrak_borclusu_firma_id_kimlik_no",
                schema: "kasabanka",
                table: "evrak_borclusu",
                columns: new[] { "firma_id", "kimlik_no" },
                unique: true,
                filter: "kimlik_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_evrak_bordrosu_firma_id_kod",
                schema: "kasabanka",
                table: "evrak_bordrosu",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evrak_hareketi_bordro_id",
                schema: "kasabanka",
                table: "evrak_hareketi",
                column: "bordro_id");

            migrationBuilder.CreateIndex(
                name: "ix_evrak_hareketi_evrak_id_sira",
                schema: "kasabanka",
                table: "evrak_hareketi",
                columns: new[] { "evrak_id", "sira" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evrak_hareketi_islem_id",
                schema: "kasabanka",
                table: "evrak_hareketi",
                column: "islem_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "borclu_hesabi",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "cek_senet_ayari",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "evrak_hareketi",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "evrak_bordrosu",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "evrak",
                schema: "kasabanka");

            migrationBuilder.DropTable(
                name: "evrak_borclusu",
                schema: "kasabanka");
        }
    }
}
