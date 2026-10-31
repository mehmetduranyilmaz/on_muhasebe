using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MaliMusavirRolu : Migration
    {
        // Mali müşavir rolünün yetkileri (bu migration anındaki katalog): tüm görüntülemeler + muhasebe aktarımı.
        private static readonly string[] _maliMusavirYetkileri =
        [
            "yonetim.degisiklik.goruntule",
            "cari.hesap.goruntule", "cari.grup.goruntule", "cari.hareket.goruntule", "cari.kapama.goruntule", "cari.rapor.goruntule",
            "stok.kart.goruntule", "stok.tanim.goruntule", "stok.hareket.goruntule", "stok.fiyat.goruntule", "stok.rapor.goruntule",
            "stok.maliyet.goruntule",
            "fatura.belge.goruntule", "fatura.irsaliye.goruntule", "fatura.teklif.goruntule", "fatura.rapor.goruntule",
            "kasabanka.hesap.goruntule", "kasabanka.islem.goruntule", "kasabanka.tanim.goruntule", "kasabanka.ekstre.goruntule",
            "kasabanka.cek.goruntule", "kasabanka.teminat.goruntule", "kasabanka.kredi.goruntule", "kasabanka.demirbas.goruntule",
            "kasabanka.rapor.goruntule",
            "muhasebe.aktarim.goruntule", "muhasebe.aktarim.ekle", "muhasebe.plan.goruntule"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "mali_musavir_mi",
                schema: "yonetim",
                table: "firma_rolu",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Hazır mali müşavir rolü işaretlenir ve muhasebe yetkileri eklenir; rolü olmayan (eski) firmalara rol açılır.
            migrationBuilder.Sql($$"""
                UPDATE yonetim.firma_rolu SET mali_musavir_mi = true, yetkiler = '{{{string.Join(",", _maliMusavirYetkileri)}}}'
                WHERE ad = 'Salt Okur (Mali Müşavir)';

                INSERT INTO yonetim.firma_rolu (id, firma_id, ad, aciklama, tam_yetkili_mi, mali_musavir_mi, yetkiler, olusturma_tarihi)
                SELECT gen_random_uuid(), f.id, 'Salt Okur (Mali Müşavir)',
                       'Tüm kayıt ve raporları görür, maliyet dahil; muhasebe fişlerini indirir. Kullanıcı limitine sayılmaz.',
                       false, true, '{{{string.Join(",", _maliMusavirYetkileri)}}}', now()
                FROM yonetim.firma f
                WHERE NOT EXISTS (SELECT 1 FROM yonetim.firma_rolu r WHERE r.firma_id = f.id AND r.ad = 'Salt Okur (Mali Müşavir)');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mali_musavir_mi",
                schema: "yonetim",
                table: "firma_rolu");
        }
    }
}
