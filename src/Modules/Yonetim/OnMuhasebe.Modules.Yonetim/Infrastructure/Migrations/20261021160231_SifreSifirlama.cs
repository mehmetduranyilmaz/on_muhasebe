using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Yonetim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SifreSifirlama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sifre_sifirlama_kodu",
                schema: "yonetim",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kullanici_id = table.Column<Guid>(type: "uuid", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sifre_sifirlama_kodu",
                schema: "yonetim");
        }
    }
}
