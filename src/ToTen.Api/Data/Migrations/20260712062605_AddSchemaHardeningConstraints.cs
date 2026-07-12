using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToTen.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemaHardeningConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_Amount_Positive",
                table: "Transactions",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Organizations_Type",
                table: "Organizations",
                sql: "\"Type\" IN ('Household', 'Business')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Offers_Amount_Positive",
                table: "Offers",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Offers_CounterAmount_Positive",
                table: "Offers",
                sql: "\"CounterAmount\" IS NULL OR \"CounterAmount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Offers_Status_Range",
                table: "Offers",
                sql: "\"Status\" BETWEEN 0 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Manifests_Status_Range",
                table: "Manifests",
                sql: "\"Status\" BETWEEN 0 AND 4");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_OrganizationId_Name",
                table: "Locations",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Listings_Price_Positive",
                table: "Listings",
                sql: "\"Price\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_Amount_Positive",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Organizations_Type",
                table: "Organizations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Offers_Amount_Positive",
                table: "Offers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Offers_CounterAmount_Positive",
                table: "Offers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Offers_Status_Range",
                table: "Offers");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Manifests_Status_Range",
                table: "Manifests");

            migrationBuilder.DropIndex(
                name: "IX_Locations_OrganizationId_Name",
                table: "Locations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Listings_Price_Positive",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");
        }
    }
}
