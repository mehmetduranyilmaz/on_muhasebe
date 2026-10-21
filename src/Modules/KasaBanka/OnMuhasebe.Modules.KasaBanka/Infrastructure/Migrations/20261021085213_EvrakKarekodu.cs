using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EvrakKarekodu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "karekod",
                schema: "kasabanka",
                table: "evrak",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_evrak_firma_id_karekod",
                schema: "kasabanka",
                table: "evrak",
                columns: new[] { "firma_id", "karekod" },
                filter: "karekod IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_evrak_firma_id_karekod",
                schema: "kasabanka",
                table: "evrak");

            migrationBuilder.DropColumn(
                name: "karekod",
                schema: "kasabanka",
                table: "evrak");
        }
    }
}
