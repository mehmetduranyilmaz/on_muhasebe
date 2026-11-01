using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Bakim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bakim");

            migrationBuilder.CreateTable(
                name: "degisiklik_kaydi",
                schema: "bakim",
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
                name: "ekipman",
                schema: "bakim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tur = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    marka = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    seri_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    uretim_yili = table.Column<int>(type: "integer", nullable: true),
                    konum = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    isyeri_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sorumlu_kullanici_id = table.Column<Guid>(type: "uuid", nullable: true),
                    demirbas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    masraf_merkezi_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sayac_birimi = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sayac = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("pk_ekipman", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bakim_plani",
                schema: "bakim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ekipman_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    yasal_mi = table.Column<bool>(type: "boolean", nullable: false),
                    kontrol_kurulusu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    periyot_gun = table.Column<int>(type: "integer", nullable: true),
                    periyot_sayac = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    son_yapilma_tarihi = table.Column<DateOnly>(type: "date", nullable: true),
                    son_yapilma_sayaci = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    sonraki_tarih = table.Column<DateOnly>(type: "date", nullable: true),
                    sonraki_sayac = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    hatirlatma_gun = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_bakim_plani", x => x.id);
                    table.ForeignKey(
                        name: "fk_bakim_plani_ekipmanlar_ekipman_id",
                        column: x => x.ekipman_id,
                        principalSchema: "bakim",
                        principalTable: "ekipman",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "is_emri",
                schema: "bakim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ekipman_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tur = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    baslik = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    bildirim_zamani = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    baslangic_zamani = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    bitis_zamani = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    makine_durdu_mu = table.Column<bool>(type: "boolean", nullable: false),
                    iscilik_tutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    dis_servis_tutari = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    dis_servis_firma = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fatura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kontrol_kurulusu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rapor_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sayac_degeri = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_is_emri", x => x.id);
                    table.ForeignKey(
                        name: "fk_is_emri_ekipman_ekipman_id",
                        column: x => x.ekipman_id,
                        principalSchema: "bakim",
                        principalTable: "ekipman",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "is_emri_parcasi",
                schema: "bakim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_emri_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stok_karti_id = table.Column<Guid>(type: "uuid", nullable: true),
                    depo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ad = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    birim = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    miktar = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    birim_fiyat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_is_emri_parcasi", x => x.id);
                    table.ForeignKey(
                        name: "fk_is_emri_parcasi_is_emirleri_is_emri_id",
                        column: x => x.is_emri_id,
                        principalSchema: "bakim",
                        principalTable: "is_emri",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bakim_plani_ekipman_id",
                schema: "bakim",
                table: "bakim_plani",
                column: "ekipman_id");

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_kayit_id",
                schema: "bakim",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_degisiklik_kaydi_firma_id_ust_kayit_id",
                schema: "bakim",
                table: "degisiklik_kaydi",
                columns: new[] { "firma_id", "ust_kayit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ekipman_firma_id_kod",
                schema: "bakim",
                table: "ekipman",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_is_emri_ekipman_id_durum",
                schema: "bakim",
                table: "is_emri",
                columns: new[] { "ekipman_id", "durum" });

            migrationBuilder.CreateIndex(
                name: "ix_is_emri_firma_id_kod",
                schema: "bakim",
                table: "is_emri",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_is_emri_parcasi_is_emri_id",
                schema: "bakim",
                table: "is_emri_parcasi",
                column: "is_emri_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bakim_plani",
                schema: "bakim");

            migrationBuilder.DropTable(
                name: "degisiklik_kaydi",
                schema: "bakim");

            migrationBuilder.DropTable(
                name: "is_emri_parcasi",
                schema: "bakim");

            migrationBuilder.DropTable(
                name: "is_emri",
                schema: "bakim");

            migrationBuilder.DropTable(
                name: "ekipman",
                schema: "bakim");
        }
    }
}
