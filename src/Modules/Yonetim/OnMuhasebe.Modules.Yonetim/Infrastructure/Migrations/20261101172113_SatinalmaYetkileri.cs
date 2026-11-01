using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SatinalmaYetkileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hazır rollere yeni modülün yetkileri. Onaylama ve kural değiştirme yönetici dışındaki hazır rollere verilmez (görevler ayrılığı).
            migrationBuilder.Sql("""
                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['satinalma.talep.goruntule','satinalma.talep.ekle','satinalma.talep.duzenle','satinalma.talep.sil','satinalma.talep.iptal','satinalma.onay.goruntule','satinalma.kural.goruntule']
                WHERE NOT tam_yetkili_mi AND ad IN ('Kullanıcı', 'Muhasebe');

                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['satinalma.talep.goruntule','satinalma.talep.ekle','satinalma.talep.duzenle','satinalma.talep.sil','satinalma.talep.iptal']
                WHERE NOT tam_yetkili_mi AND ad = 'Satın Alma';

                UPDATE yonetim.firma_rolu SET yetkiler = yetkiler || ARRAY['satinalma.talep.goruntule','satinalma.onay.goruntule','satinalma.kural.goruntule']
                WHERE mali_musavir_mi AND NOT ('satinalma.talep.goruntule' = ANY(yetkiler));
                """);


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
