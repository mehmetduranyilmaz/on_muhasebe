using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Cari.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KapamaVeDovizIzni : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "farkli_dovizde_islem",
                schema: "cari",
                table: "cari_hesap",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "elle_kapama_mi",
                schema: "cari",
                table: "cari_hareket",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "cari_kapama",
                schema: "cari",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cari_hesap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borc_hareket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alacak_hareket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    firma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cari_kapama", x => x.id);
                    table.ForeignKey(
                        name: "fk_cari_kapama_cari_hareket_alacak_hareket_id",
                        column: x => x.alacak_hareket_id,
                        principalSchema: "cari",
                        principalTable: "cari_hareket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cari_kapama_cari_hareket_borc_hareket_id",
                        column: x => x.borc_hareket_id,
                        principalSchema: "cari",
                        principalTable: "cari_hareket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cari_kapama_alacak_hareket_id",
                schema: "cari",
                table: "cari_kapama",
                column: "alacak_hareket_id");

            migrationBuilder.CreateIndex(
                name: "ix_cari_kapama_borc_hareket_id",
                schema: "cari",
                table: "cari_kapama",
                column: "borc_hareket_id");

            migrationBuilder.CreateIndex(
                name: "ix_cari_kapama_firma_id_cari_hesap_id",
                schema: "cari",
                table: "cari_kapama",
                columns: new[] { "firma_id", "cari_hesap_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cari_kapama",
                schema: "cari");

            migrationBuilder.DropColumn(
                name: "farkli_dovizde_islem",
                schema: "cari",
                table: "cari_hesap");

            migrationBuilder.DropColumn(
                name: "elle_kapama_mi",
                schema: "cari",
                table: "cari_hareket");
        }
    }
}
