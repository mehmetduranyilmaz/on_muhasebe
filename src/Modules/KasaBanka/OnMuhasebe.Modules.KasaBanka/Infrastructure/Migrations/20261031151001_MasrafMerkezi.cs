using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MasrafMerkezi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "masraf_merkezi_id",
                schema: "kasabanka",
                table: "gelir_gider_kaydi",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "masraf_merkezi",
                schema: "kasabanka",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_masraf_merkezi", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gelir_gider_kaydi_masraf_merkezi_id",
                schema: "kasabanka",
                table: "gelir_gider_kaydi",
                column: "masraf_merkezi_id");

            migrationBuilder.CreateIndex(
                name: "ix_masraf_merkezi_firma_id_kod",
                schema: "kasabanka",
                table: "masraf_merkezi",
                columns: new[] { "firma_id", "kod" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_gelir_gider_kaydi_masraf_merkezleri_masraf_merkezi_id",
                schema: "kasabanka",
                table: "gelir_gider_kaydi",
                column: "masraf_merkezi_id",
                principalSchema: "kasabanka",
                principalTable: "masraf_merkezi",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_gelir_gider_kaydi_masraf_merkezleri_masraf_merkezi_id",
                schema: "kasabanka",
                table: "gelir_gider_kaydi");

            migrationBuilder.DropTable(
                name: "masraf_merkezi",
                schema: "kasabanka");

            migrationBuilder.DropIndex(
                name: "ix_gelir_gider_kaydi_masraf_merkezi_id",
                schema: "kasabanka",
                table: "gelir_gider_kaydi");

            migrationBuilder.DropColumn(
                name: "masraf_merkezi_id",
                schema: "kasabanka",
                table: "gelir_gider_kaydi");
        }
    }
}
