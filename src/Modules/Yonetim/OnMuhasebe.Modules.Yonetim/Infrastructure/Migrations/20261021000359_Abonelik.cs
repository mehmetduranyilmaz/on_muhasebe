using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Abonelik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "aciklama",
                schema: "yonetim",
                table: "paket",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "aylik_ucret",
                schema: "yonetim",
                table: "paket",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "kullanici_limiti",
                schema: "yonetim",
                table: "paket",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "satista_mi",
                schema: "yonetim",
                table: "paket",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sira",
                schema: "yonetim",
                table: "paket",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "yillik_ucret",
                schema: "yonetim",
                table: "paket",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "odeme_bildirimi",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ay_sayisi = table.Column<int>(type: "integer", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    odeme_tarihi = table.Column<DateOnly>(type: "date", nullable: false),
                    gonderen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    durum = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sonuc_notu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sonuc_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    yeni_bitis_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_odeme_bildirimi", x => x.id);
                    table.ForeignKey(
                        name: "fk_odeme_bildirimi_paketler_paket_id",
                        column: x => x.paket_id,
                        principalSchema: "yonetim",
                        principalTable: "paket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sistem_ayari",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    odeme_alicisi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    odeme_bankasi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    odeme_iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    odeme_notu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sistem_ayari", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_odeme_bildirimi_durum",
                schema: "yonetim",
                table: "odeme_bildirimi",
                column: "durum");

            migrationBuilder.CreateIndex(
                name: "ix_odeme_bildirimi_firma_id_olusturma_tarihi",
                schema: "yonetim",
                table: "odeme_bildirimi",
                columns: new[] { "firma_id", "olusturma_tarihi" });

            migrationBuilder.CreateIndex(
                name: "ix_odeme_bildirimi_paket_id",
                schema: "yonetim",
                table: "odeme_bildirimi",
                column: "paket_id");

            // Elle eklendi: satış paketleri. Ücretler boş; sistem yöneticisi Paketler ekranından girer. "basic" kodu korunup Temel adını alır.
            migrationBuilder.Sql("""
                UPDATE yonetim.paket SET ad = 'Temel', aciklama = 'Cari, stok, fatura ve kasa/banka; küçük işletmeler için.',
                    kullanici_limiti = 2, satista_mi = true, sira = 1
                WHERE kod = 'basic';
                UPDATE yonetim.paket SET sira = 0 WHERE kod = 'deneme';
                """);
            migrationBuilder.InsertData(
                schema: "yonetim",
                table: "paket",
                columns: new[] { "id", "kod", "ad", "aciklama", "tum_moduller_mi", "moduller", "kullanici_limiti", "satista_mi", "sira", "olusturma_tarihi" },
                values: new object[,]
                {
                    {
                        new Guid("019a0000-0000-7000-8000-000000000003"), "profesyonel", "Profesyonel", "Temel pakete ek olarak daha fazla kullanıcı.",
                        false, new[] { "cari", "stok", "fatura", "kasabanka" }, 5, true, 2, new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        new Guid("019a0000-0000-7000-8000-000000000004"), "kurumsal", "Kurumsal", "Tüm modüller, sınırsız kullanıcı.",
                        true, Array.Empty<string>(), null!, true, 3, new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM yonetim.paket WHERE kod IN ('profesyonel', 'kurumsal'); UPDATE yonetim.paket SET ad = 'Basic' WHERE kod = 'basic';");

            migrationBuilder.DropTable(
                name: "odeme_bildirimi",
                schema: "yonetim");

            migrationBuilder.DropTable(
                name: "sistem_ayari",
                schema: "yonetim");

            migrationBuilder.DropColumn(
                name: "aciklama",
                schema: "yonetim",
                table: "paket");

            migrationBuilder.DropColumn(
                name: "aylik_ucret",
                schema: "yonetim",
                table: "paket");

            migrationBuilder.DropColumn(
                name: "kullanici_limiti",
                schema: "yonetim",
                table: "paket");

            migrationBuilder.DropColumn(
                name: "satista_mi",
                schema: "yonetim",
                table: "paket");

            migrationBuilder.DropColumn(
                name: "sira",
                schema: "yonetim",
                table: "paket");

            migrationBuilder.DropColumn(
                name: "yillik_ucret",
                schema: "yonetim",
                table: "paket");
        }
    }
}
