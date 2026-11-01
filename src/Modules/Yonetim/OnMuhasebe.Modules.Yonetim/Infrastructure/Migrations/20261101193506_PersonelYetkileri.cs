using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersonelYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hazır rollere yeni modül: özlük tutarları Kullanıcı rolüne verilmez (KVKK), Muhasebe tam alır, mali müşavir görüntüler.
            migrationBuilder.Sql("""
                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['personel.kart.goruntule','personel.kart.ekle','personel.kart.duzenle','personel.kart.sil','personel.zimmet.goruntule','personel.zimmet.ekle','personel.zimmet.duzenle','personel.zimmet.sil','personel.kendi.goruntule','personel.kendi.ekle']
                WHERE NOT tam_yetkili_mi AND ad IN ('Kullanıcı', 'Muhasebe');

                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['personel.maas.goruntule','personel.maas.ekle','personel.maas.duzenle','personel.maas.sil','personel.maas.onayla','personel.maas.iptal','personel.avans.goruntule','personel.avans.ekle','personel.avans.iptal','personel.masraf.goruntule','personel.masraf.ekle','personel.masraf.duzenle','personel.masraf.sil','personel.masraf.onayla']
                WHERE NOT tam_yetkili_mi AND ad = 'Muhasebe';

                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['personel.kart.goruntule','personel.maas.goruntule','personel.avans.goruntule','personel.masraf.goruntule','personel.zimmet.goruntule','personel.kendi.goruntule']
                WHERE mali_musavir_mi AND NOT ('personel.kart.goruntule' = ANY(yetkiler));
                """);


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
