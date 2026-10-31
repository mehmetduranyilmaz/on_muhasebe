using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.Fatura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Izlenebilirlik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lot_no",
                schema: "fatura",
                table: "irsaliye_satiri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "seri_nolari",
                schema: "fatura",
                table: "irsaliye_satiri",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "son_kullanma_tarihi",
                schema: "fatura",
                table: "irsaliye_satiri",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lot_no",
                schema: "fatura",
                table: "fatura_satiri",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "seri_nolari",
                schema: "fatura",
                table: "fatura_satiri",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "son_kullanma_tarihi",
                schema: "fatura",
                table: "fatura_satiri",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lot_no",
                schema: "fatura",
                table: "irsaliye_satiri");

            migrationBuilder.DropColumn(
                name: "seri_nolari",
                schema: "fatura",
                table: "irsaliye_satiri");

            migrationBuilder.DropColumn(
                name: "son_kullanma_tarihi",
                schema: "fatura",
                table: "irsaliye_satiri");

            migrationBuilder.DropColumn(
                name: "lot_no",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "seri_nolari",
                schema: "fatura",
                table: "fatura_satiri");

            migrationBuilder.DropColumn(
                name: "son_kullanma_tarihi",
                schema: "fatura",
                table: "fatura_satiri");
        }
    }
}
