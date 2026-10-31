using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RolVeYetki : Migration
    {
        // Mevcut firmalara iki rol açılır: Yönetici (tam yetkili) ve Kullanıcı. Kullanıcı, eski "Kullanici" rolünün kapsamıdır:
        // yönetim ekranları dışındaki tüm işlemler (bu migration anındaki modül yetkilerinin tamamı). Üyelik ve davetler rol adından role bağlanır.
        private static readonly string[] _kullaniciYetkileri =
        [
            "cari.grup.duzenle", "cari.grup.ekle", "cari.grup.goruntule", "cari.grup.sil",
            "cari.hareket.ekle", "cari.hareket.goruntule", "cari.hareket.iptal",
            "cari.hesap.duzenle", "cari.hesap.ekle", "cari.hesap.goruntule", "cari.hesap.sil",
            "cari.kapama.duzenle", "cari.kapama.goruntule", "cari.rapor.goruntule",
            "fatura.belge.duzenle", "fatura.belge.ekle", "fatura.belge.goruntule", "fatura.belge.iptal", "fatura.belge.onayla", "fatura.belge.sil",
            "fatura.irsaliye.duzenle", "fatura.irsaliye.ekle", "fatura.irsaliye.goruntule", "fatura.irsaliye.iptal", "fatura.irsaliye.onayla",
            "fatura.irsaliye.sil", "fatura.rapor.goruntule",
            "fatura.teklif.duzenle", "fatura.teklif.ekle", "fatura.teklif.goruntule", "fatura.teklif.iptal", "fatura.teklif.onayla", "fatura.teklif.sil",
            "kasabanka.cek.duzenle", "kasabanka.cek.ekle", "kasabanka.cek.goruntule", "kasabanka.cek.iptal", "kasabanka.cek.onayla", "kasabanka.cek.sil",
            "kasabanka.demirbas.duzenle", "kasabanka.demirbas.ekle", "kasabanka.demirbas.goruntule", "kasabanka.demirbas.sil",
            "kasabanka.ekstre.duzenle", "kasabanka.ekstre.ekle", "kasabanka.ekstre.goruntule", "kasabanka.ekstre.sil",
            "kasabanka.hesap.duzenle", "kasabanka.hesap.ekle", "kasabanka.hesap.goruntule", "kasabanka.hesap.sil",
            "kasabanka.islem.ekle", "kasabanka.islem.goruntule", "kasabanka.islem.iptal",
            "kasabanka.kredi.ekle", "kasabanka.kredi.goruntule", "kasabanka.kredi.iptal", "kasabanka.rapor.goruntule",
            "kasabanka.tanim.duzenle", "kasabanka.tanim.ekle", "kasabanka.tanim.goruntule", "kasabanka.tanim.sil",
            "kasabanka.teminat.duzenle", "kasabanka.teminat.ekle", "kasabanka.teminat.goruntule", "kasabanka.teminat.sil",
            "stok.fiyat.duzenle", "stok.fiyat.ekle", "stok.fiyat.goruntule", "stok.fiyat.sil",
            "stok.hareket.ekle", "stok.hareket.goruntule", "stok.hareket.iptal",
            "stok.kart.duzenle", "stok.kart.ekle", "stok.kart.goruntule", "stok.kart.sil",
            "stok.maliyet.goruntule", "stok.rapor.goruntule",
            "stok.tanim.duzenle", "stok.tanim.ekle", "stok.tanim.goruntule", "stok.tanim.sil"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "firma_rolu",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tam_yetkili_mi = table.Column<bool>(type: "boolean", nullable: false),
                    yetkiler = table.Column<List<string>>(type: "text[]", nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_firma_rolu", x => x.id);
                    table.ForeignKey(
                        name: "fk_firma_rolu_firma_firma_id",
                        column: x => x.firma_id,
                        principalSchema: "yonetim",
                        principalTable: "firma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<Guid>(name: "rol_id", schema: "yonetim", table: "firma_kullanicisi", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "rol_id", schema: "yonetim", table: "firma_daveti", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<List<string>>(name: "ek_yetkiler", schema: "yonetim", table: "firma_kullanicisi", type: "text[]",
                nullable: false, defaultValueSql: "'{}'");
            migrationBuilder.AddColumn<List<string>>(name: "yasak_yetkiler", schema: "yonetim", table: "firma_kullanicisi", type: "text[]",
                nullable: false, defaultValueSql: "'{}'");

            migrationBuilder.Sql($$"""
                INSERT INTO yonetim.firma_rolu (id, firma_id, ad, aciklama, tam_yetkili_mi, yetkiler, olusturma_tarihi)
                SELECT gen_random_uuid(), f.id, 'Yönetici', 'Tüm yetkiler; kiralanan yeni modüller de kendiliğinden dahil olur.', true, '{}', now()
                FROM yonetim.firma f;

                INSERT INTO yonetim.firma_rolu (id, firma_id, ad, aciklama, tam_yetkili_mi, yetkiler, olusturma_tarihi)
                SELECT gen_random_uuid(), f.id, 'Kullanıcı', 'Yönetim ekranları dışında tüm işlemler.', false,
                       '{{{string.Join(",", _kullaniciYetkileri)}}}', now()
                FROM yonetim.firma f;

                UPDATE yonetim.firma_kullanicisi u SET rol_id = r.id FROM yonetim.firma_rolu r
                WHERE r.firma_id = u.firma_id AND r.tam_yetkili_mi = (u.rol = 'Yonetici');

                UPDATE yonetim.firma_daveti d SET rol_id = r.id FROM yonetim.firma_rolu r
                WHERE r.firma_id = d.firma_id AND r.tam_yetkili_mi = (d.rol = 'Yonetici');
                """);

            migrationBuilder.DropColumn(name: "rol", schema: "yonetim", table: "firma_kullanicisi");
            migrationBuilder.DropColumn(name: "rol", schema: "yonetim", table: "firma_daveti");
            migrationBuilder.AlterColumn<Guid>(name: "rol_id", schema: "yonetim", table: "firma_kullanicisi", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "rol_id", schema: "yonetim", table: "firma_daveti", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_firma_kullanicisi_rol_id",
                schema: "yonetim",
                table: "firma_kullanicisi",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "ix_firma_daveti_rol_id",
                schema: "yonetim",
                table: "firma_daveti",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "ix_firma_rolu_firma_id_ad",
                schema: "yonetim",
                table: "firma_rolu",
                columns: new[] { "firma_id", "ad" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_firma_daveti_roller_rol_id",
                schema: "yonetim",
                table: "firma_daveti",
                column: "rol_id",
                principalSchema: "yonetim",
                principalTable: "firma_rolu",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_firma_kullanicisi_roller_rol_id",
                schema: "yonetim",
                table: "firma_kullanicisi",
                column: "rol_id",
                principalSchema: "yonetim",
                principalTable: "firma_rolu",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "rol", schema: "yonetim", table: "firma_kullanicisi", type: "character varying(20)",
                maxLength: 20, nullable: false, defaultValue: "Kullanici");
            migrationBuilder.AddColumn<string>(name: "rol", schema: "yonetim", table: "firma_daveti", type: "character varying(20)",
                maxLength: 20, nullable: false, defaultValue: "Kullanici");

            migrationBuilder.Sql("""
                UPDATE yonetim.firma_kullanicisi u SET rol = 'Yonetici' FROM yonetim.firma_rolu r WHERE r.id = u.rol_id AND r.tam_yetkili_mi;
                UPDATE yonetim.firma_daveti d SET rol = 'Yonetici' FROM yonetim.firma_rolu r WHERE r.id = d.rol_id AND r.tam_yetkili_mi;
                """);

            migrationBuilder.DropForeignKey(name: "fk_firma_daveti_roller_rol_id", schema: "yonetim", table: "firma_daveti");
            migrationBuilder.DropForeignKey(name: "fk_firma_kullanicisi_roller_rol_id", schema: "yonetim", table: "firma_kullanicisi");
            migrationBuilder.DropTable(name: "firma_rolu", schema: "yonetim");
            migrationBuilder.DropIndex(name: "ix_firma_kullanicisi_rol_id", schema: "yonetim", table: "firma_kullanicisi");
            migrationBuilder.DropIndex(name: "ix_firma_daveti_rol_id", schema: "yonetim", table: "firma_daveti");
            migrationBuilder.DropColumn(name: "ek_yetkiler", schema: "yonetim", table: "firma_kullanicisi");
            migrationBuilder.DropColumn(name: "rol_id", schema: "yonetim", table: "firma_kullanicisi");
            migrationBuilder.DropColumn(name: "yasak_yetkiler", schema: "yonetim", table: "firma_kullanicisi");
            migrationBuilder.DropColumn(name: "rol_id", schema: "yonetim", table: "firma_daveti");
        }
    }
}
