using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Stok.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Izlenebilirlik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "izleme",
                schema: "stok",
                table: "stok_karti",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Yok");

            migrationBuilder.AddColumn<bool>(
                name: "skt_takibi",
                schema: "stok",
                table: "stok_karti",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "lot_no",
                schema: "stok",
                table: "stok_hareket",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "son_kullanma_tarihi",
                schema: "stok",
                table: "stok_hareket",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stok_hareket_seri",
                schema: "stok",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hareket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stok_karti_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seri_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    olusturan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    guncelleyen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stok_hareket_seri", x => x.id);
                    table.ForeignKey(
                        name: "fk_stok_hareket_seri_stok_hareket_hareket_id",
                        column: x => x.hareket_id,
                        principalSchema: "stok",
                        principalTable: "stok_hareket",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_firma_id_stok_karti_id_lot_no",
                schema: "stok",
                table: "stok_hareket",
                columns: new[] { "firma_id", "stok_karti_id", "lot_no" },
                filter: "lot_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_seri_hareket_id",
                schema: "stok",
                table: "stok_hareket_seri",
                column: "hareket_id");

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_seri_seri_no",
                schema: "stok",
                table: "stok_hareket_seri",
                column: "seri_no");

            migrationBuilder.CreateIndex(
                name: "ix_stok_hareket_seri_stok_karti_id_seri_no",
                schema: "stok",
                table: "stok_hareket_seri",
                columns: new[] { "stok_karti_id", "seri_no" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stok_hareket_seri",
                schema: "stok");

            migrationBuilder.DropIndex(
                name: "ix_stok_hareket_firma_id_stok_karti_id_lot_no",
                schema: "stok",
                table: "stok_hareket");

            migrationBuilder.DropColumn(
                name: "izleme",
                schema: "stok",
                table: "stok_karti");

            migrationBuilder.DropColumn(
                name: "skt_takibi",
                schema: "stok",
                table: "stok_karti");

            migrationBuilder.DropColumn(
                name: "lot_no",
                schema: "stok",
                table: "stok_hareket");

            migrationBuilder.DropColumn(
                name: "son_kullanma_tarihi",
                schema: "stok",
                table: "stok_hareket");
        }
    }
}
