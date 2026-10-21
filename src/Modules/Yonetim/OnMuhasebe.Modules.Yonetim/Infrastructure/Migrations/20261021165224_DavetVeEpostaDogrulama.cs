using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DavetVeEpostaDogrulama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Şifre sıfırlama kodları en fazla 1 saat geçerli geçici kayıtlardır; genel kullanici_kodu tablosuna taşınmaz.
            migrationBuilder.DropTable(
                name: "sifre_sifirlama_kodu",
                schema: "yonetim");

            // Bu sürümden önce açılmış hesaplar doğrulanmış sayılır; yeni kayıtlarda uygulama değeri açıkça false yazar.
            migrationBuilder.AddColumn<bool>(
                name: "eposta_dogrulandi_mi",
                schema: "yonetim",
                table: "kullanici",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "firma_daveti",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    eposta = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kod_ozeti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bitis_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    son_gonderim_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    kabul_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    iptal_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firma_daveti", x => x.id);
                    table.ForeignKey(
                        name: "fk_firma_daveti_firma_firma_id",
                        column: x => x.firma_id,
                        principalSchema: "yonetim",
                        principalTable: "firma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kullanici_kodu",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amac = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kod_ozeti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bitis_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    kullanim_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kullanici_kodu", x => x.id);
                    table.ForeignKey(
                        name: "fk_kullanici_kodu_kullanici_kullanici_id",
                        column: x => x.kullanici_id,
                        principalSchema: "yonetim",
                        principalTable: "kullanici",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_firma_daveti_firma_id_eposta",
                schema: "yonetim",
                table: "firma_daveti",
                columns: new[] { "firma_id", "eposta" });

            migrationBuilder.CreateIndex(
                name: "ix_firma_daveti_kod_ozeti",
                schema: "yonetim",
                table: "firma_daveti",
                column: "kod_ozeti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kullanici_kodu_kod_ozeti",
                schema: "yonetim",
                table: "kullanici_kodu",
                column: "kod_ozeti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kullanici_kodu_kullanici_id",
                schema: "yonetim",
                table: "kullanici_kodu",
                column: "kullanici_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "firma_daveti",
                schema: "yonetim");

            migrationBuilder.DropTable(
                name: "kullanici_kodu",
                schema: "yonetim");

            migrationBuilder.DropColumn(
                name: "eposta_dogrulandi_mi",
                schema: "yonetim",
                table: "kullanici");

            migrationBuilder.CreateTable(
                name: "sifre_sifirlama_kodu",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bitis_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kod_ozeti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanim_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sifre_sifirlama_kodu", x => x.id);
                    table.ForeignKey(
                        name: "fk_sifre_sifirlama_kodu_kullanici_kullanici_id",
                        column: x => x.kullanici_id,
                        principalSchema: "yonetim",
                        principalTable: "kullanici",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sifre_sifirlama_kodu_kod_ozeti",
                schema: "yonetim",
                table: "sifre_sifirlama_kodu",
                column: "kod_ozeti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sifre_sifirlama_kodu_kullanici_id",
                schema: "yonetim",
                table: "sifre_sifirlama_kodu",
                column: "kullanici_id");
        }
    }
}
