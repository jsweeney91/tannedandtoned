using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tanningTribe.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReferralCodesPerEmailAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReferralCodes_Orders_OwnerOrderId",
                table: "ReferralCodes");

            migrationBuilder.DropIndex(
                name: "IX_ReferralCodes_OwnerOrderId",
                table: "ReferralCodes");

            migrationBuilder.AlterColumn<int>(
                name: "OwnerOrderId",
                table: "ReferralCodes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "PostageAmount",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.CreateTable(
                name: "StoreSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DefaultReferralDiscountPercent = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSettings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // One code per email: normalise emails, merge usage into the oldest code and remove the rest.
            migrationBuilder.Sql("UPDATE ReferralCodes SET OwnerEmail = LOWER(TRIM(OwnerEmail));");
            migrationBuilder.Sql(@"
UPDATE ReferralCodes r
JOIN (SELECT OwnerEmail, MIN(Id) AS KeepId, SUM(TimesUsed) AS Uses FROM ReferralCodes GROUP BY OwnerEmail) g
  ON r.Id = g.KeepId
SET r.TimesUsed = g.Uses;");
            migrationBuilder.Sql(@"
DELETE r FROM ReferralCodes r
JOIN (SELECT OwnerEmail, MIN(Id) AS KeepId FROM ReferralCodes GROUP BY OwnerEmail) g
  ON r.OwnerEmail = g.OwnerEmail AND r.Id <> g.KeepId;");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_OwnerEmail",
                table: "ReferralCodes",
                column: "OwnerEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_OwnerOrderId",
                table: "ReferralCodes",
                column: "OwnerOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReferralCodes_Orders_OwnerOrderId",
                table: "ReferralCodes",
                column: "OwnerOrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReferralCodes_Orders_OwnerOrderId",
                table: "ReferralCodes");

            migrationBuilder.DropTable(
                name: "StoreSettings");

            migrationBuilder.DropIndex(
                name: "IX_ReferralCodes_OwnerEmail",
                table: "ReferralCodes");

            migrationBuilder.DropIndex(
                name: "IX_ReferralCodes_OwnerOrderId",
                table: "ReferralCodes");

            migrationBuilder.AlterColumn<int>(
                name: "OwnerOrderId",
                table: "ReferralCodes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PostageAmount",
                table: "Orders",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_OwnerOrderId",
                table: "ReferralCodes",
                column: "OwnerOrderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReferralCodes_Orders_OwnerOrderId",
                table: "ReferralCodes",
                column: "OwnerOrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
