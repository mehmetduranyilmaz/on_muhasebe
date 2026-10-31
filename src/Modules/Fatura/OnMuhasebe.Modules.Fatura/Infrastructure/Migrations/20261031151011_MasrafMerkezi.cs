using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MasrafMerkezi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "masraf_merkezi_id",
                schema: "fatura",
                table: "fatura",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_fatura_masraf_merkezi_id",
                schema: "fatura",
                table: "fatura",
                column: "masraf_merkezi_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fatura_masraf_merkezi_id",
                schema: "fatura",
                table: "fatura");

            migrationBuilder.DropColumn(
                name: "masraf_merkezi_id",
                schema: "fatura",
                table: "fatura");
        }
    }
}
