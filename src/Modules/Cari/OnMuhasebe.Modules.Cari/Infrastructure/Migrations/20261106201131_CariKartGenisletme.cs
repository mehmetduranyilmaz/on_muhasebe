using System;
using Microsoft.EntityFrameworkCore.Migrations;
using OnMuhasebe.Shared.Contracts.Bankalar;

#nullable disable

namespace OnMuhasebe.Modules.Cari.Infrastructure.Migrations
{
    // Elle düzenlendi: EF, yetkili_ad/yetkili_telefon kolonlarını soyad/faks olarak yeniden adlandırmayı önermişti (veri yanlış alana
    // taşınırdı). Sıra: yeni kolon ve tablolar → mevcut verinin taşınması → eski kolonların kaldırılması.
    public partial class CariKartGenisletme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            void Metin(string ad, int uzunluk) => migrationBuilder.AddColumn<string>(
                name: ad, schema: "cari", table: "cari_hesap", type: $"character varying({uzunluk})", maxLength: uzunluk, nullable: true);

            Metin("ad", 100);
            Metin("soyad", 100);
            Metin("posta_kodu", 10);
            Metin("cep_telefonu", 30);
            Metin("faks", 30);
            Metin("web_sitesi", 200);
            Metin("e_fatura_etiketi", 100);
            Metin("varsayilan_kdv_istisna_kodu", 5);
            Metin("varsayilan_tevkifat_kodu", 5);

            migrationBuilder.AddColumn<bool>(
                name: "e_fatura_mukellefi_mi", schema: "cari", table: "cari_hesap", type: "boolean", nullable: false, defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ulke", schema: "cari", table: "cari_hesap", type: "character varying(60)", maxLength: 60, nullable: false, defaultValue: "Türkiye");

            migrationBuilder.CreateTable(
                name: "cari_adres",
                schema: "cari",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    baslik = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ad_soyad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ulke = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    il = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ilce = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    adres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    posta_kodu = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cari_adres", x => x.id);
                    table.ForeignKey(
                        name: "fk_cari_adres_cari_hesaplar_cari_hesap_id",
                        column: x => x.cari_hesap_id,
                        principalSchema: "cari",
                        principalTable: "cari_hesap",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cari_banka_hesabi",
                schema: "cari",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    banka_adi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sube = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    hesap_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    para_birimi = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cari_banka_hesabi", x => x.id);
                    table.ForeignKey(
                        name: "fk_cari_banka_hesabi_cari_hesaplar_cari_hesap_id",
                        column: x => x.cari_hesap_id,
                        principalSchema: "cari",
                        principalTable: "cari_hesap",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cari_ilgili_kisi",
                schema: "cari",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    ad_soyad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gorev = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    eposta = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cari_ilgili_kisi", x => x.id);
                    table.ForeignKey(
                        name: "fk_cari_ilgili_kisi_cari_hesaplar_cari_hesap_id",
                        column: x => x.cari_hesap_id,
                        principalSchema: "cari",
                        principalTable: "cari_hesap",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cari_adres_cari_hesap_id_sira",
                schema: "cari",
                table: "cari_adres",
                columns: new[] { "cari_hesap_id", "sira" });

            migrationBuilder.CreateIndex(
                name: "ix_cari_banka_hesabi_cari_hesap_id_sira",
                schema: "cari",
                table: "cari_banka_hesabi",
                columns: new[] { "cari_hesap_id", "sira" });

            migrationBuilder.CreateIndex(
                name: "ix_cari_banka_hesabi_iban",
                schema: "cari",
                table: "cari_banka_hesabi",
                column: "iban");

            migrationBuilder.CreateIndex(
                name: "ix_cari_ilgili_kisi_cari_hesap_id_sira",
                schema: "cari",
                table: "cari_ilgili_kisi",
                columns: new[] { "cari_hesap_id", "sira" });

            // Mevcut tek yetkili → ilk ilgili kişi.
            migrationBuilder.Sql("""
                INSERT INTO cari.cari_ilgili_kisi (id, cari_hesap_id, sira, ad_soyad, telefon, olusturma_tarihi)
                SELECT uuidv7(), id, 1, COALESCE(yetkili_ad, 'Yetkili'), yetkili_telefon, now()
                FROM cari.cari_hesap
                WHERE yetkili_ad IS NOT NULL OR yetkili_telefon IS NOT NULL;
                """);

            // Mevcut tek IBAN → ilk banka hesabı; banka adı IBAN'daki 5 haneli banka kodundan (bulunamazsa "Banka").
            var bankalar = string.Join(", ", Bankalar.Tumu.Select(b => $"({int.Parse(b.Kod)}, '{b.Ad.Replace("'", "''")}')"));
            migrationBuilder.Sql($$"""
                INSERT INTO cari.cari_banka_hesabi (id, cari_hesap_id, sira, banka_adi, iban, para_birimi, olusturma_tarihi)
                SELECT uuidv7(), c.id, 1, COALESCE(b.ad, 'Banka'), c.iban, c.para_birimi, now()
                FROM cari.cari_hesap c
                LEFT JOIN (VALUES {{bankalar}}) AS b(kod, ad)
                       ON substring(c.iban from 5 for 5) ~ '^[0-9]{5}$' AND substring(c.iban from 5 for 5)::int = b.kod
                WHERE c.iban IS NOT NULL;
                """);

            // Şahıs carilerde ad/soyad ünvandan ayrılır: son kelime soyad, öncesi ad. Kullanıcı karttan düzeltebilir.
            migrationBuilder.Sql("""
                UPDATE cari.cari_hesap SET
                    ad = left(CASE WHEN btrim(unvan) ~ '\s' THEN regexp_replace(btrim(unvan), '\s+\S+$', '') ELSE btrim(unvan) END, 100),
                    soyad = CASE WHEN btrim(unvan) ~ '\s' THEN left(substring(btrim(unvan) from '(\S+)$'), 100) END
                WHERE kisi_tipi = 'Sahis';
                """);

            migrationBuilder.DropColumn(name: "iban", schema: "cari", table: "cari_hesap");
            migrationBuilder.DropColumn(name: "yetkili_ad", schema: "cari", table: "cari_hesap");
            migrationBuilder.DropColumn(name: "yetkili_telefon", schema: "cari", table: "cari_hesap");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "iban", schema: "cari", table: "cari_hesap", type: "character varying(34)", maxLength: 34, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "yetkili_ad", schema: "cari", table: "cari_hesap", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "yetkili_telefon", schema: "cari", table: "cari_hesap", type: "character varying(30)", maxLength: 30, nullable: true);

            migrationBuilder.Sql("""
                UPDATE cari.cari_hesap c SET yetkili_ad = k.ad_soyad, yetkili_telefon = k.telefon
                FROM cari.cari_ilgili_kisi k WHERE k.cari_hesap_id = c.id AND k.sira = 1;
                UPDATE cari.cari_hesap c SET iban = h.iban
                FROM cari.cari_banka_hesabi h WHERE h.cari_hesap_id = c.id AND h.sira = 1;
                """);

            migrationBuilder.DropTable(name: "cari_adres", schema: "cari");
            migrationBuilder.DropTable(name: "cari_banka_hesabi", schema: "cari");
            migrationBuilder.DropTable(name: "cari_ilgili_kisi", schema: "cari");

            foreach (var kolon in new[]
                     {
                         "ad", "soyad", "posta_kodu", "cep_telefonu", "faks", "web_sitesi", "e_fatura_etiketi", "e_fatura_mukellefi_mi",
                         "ulke", "varsayilan_kdv_istisna_kodu", "varsayilan_tevkifat_kodu"
                     })
            {
                migrationBuilder.DropColumn(name: kolon, schema: "cari", table: "cari_hesap");
            }
        }
    }
}
