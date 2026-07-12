using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToTen.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeManifestOrganizationIdRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Manifests_Organizations_OrganizationId",
                table: "Manifests");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Manifests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Manifests_Organizations_OrganizationId",
                table: "Manifests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Manifests_Organizations_OrganizationId",
                table: "Manifests");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "Manifests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Manifests_Organizations_OrganizationId",
                table: "Manifests",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id");
        }
    }
}
