using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnMuhasebe.Modules.KasaBanka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EkstreEslestirme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "evrak_id",
                schema: "kasabanka",
                table: "ekstre_satiri",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evrak_no",
                schema: "kasabanka",
                table: "ekstre_satiri",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "kapatilacak_belge_id",
                schema: "kasabanka",
                table: "ekstre_satiri",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kapatilacak_belge_no",
                schema: "kasabanka",
                table: "ekstre_satiri",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hesap_uyarisi",
                schema: "kasabanka",
                table: "ekstre_aktarimi",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "kapanis_bakiyesi",
                schema: "kasabanka",
                table: "ekstre_aktarimi",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "evrak_id",
                schema: "kasabanka",
                table: "ekstre_satiri");

            migrationBuilder.DropColumn(
                name: "evrak_no",
                schema: "kasabanka",
                table: "ekstre_satiri");

            migrationBuilder.DropColumn(
                name: "kapatilacak_belge_id",
                schema: "kasabanka",
                table: "ekstre_satiri");

            migrationBuilder.DropColumn(
                name: "kapatilacak_belge_no",
                schema: "kasabanka",
                table: "ekstre_satiri");

            migrationBuilder.DropColumn(
                name: "hesap_uyarisi",
                schema: "kasabanka",
                table: "ekstre_aktarimi");

            migrationBuilder.DropColumn(
                name: "kapanis_bakiyesi",
                schema: "kasabanka",
                table: "ekstre_aktarimi");
        }
    }
}
