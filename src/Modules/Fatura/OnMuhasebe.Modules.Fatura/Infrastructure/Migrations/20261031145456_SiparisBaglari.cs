using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SiparisBaglari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "siparis_satiri_id",
                schema: "fatura",
                table: "irsaliye_satiri",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "siparis_satiri_id",
                schema: "fatura",
                table: "fatura_satiri",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_irsaliye_satiri_siparis_satiri_id",
                schema: "fatura",
                table: "irsaliye_satiri",
                column: "siparis_satiri_id");

            migrationBuilder.CreateIndex(
                name: "ix_fatura_satiri_siparis_satiri_id",
                schema: "fatura",
                table: "fatura_satiri",
                column: "siparis_satiri_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_irsaliye_satiri_siparis_satiri_id",
                schema: "fatura",
                table: "irsaliye_satiri");

            migrationBuilder.DropIndex(
                name: "ix_fatura_satiri_siparis_satiri_id",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "siparis_satiri_id",
                schema: "fatura",
                table: "irsaliye_satiri");

            migrationBuilder.DropColumn(
                name: "siparis_satiri_id",
                schema: "fatura",
                table: "fatura_satiri");
        }
    }
}
