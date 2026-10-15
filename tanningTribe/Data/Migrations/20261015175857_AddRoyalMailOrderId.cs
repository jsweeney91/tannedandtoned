using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tanningTribe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoyalMailOrderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoyalMailOrderId",
                table: "Orders",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoyalMailOrderId",
                table: "Orders");
        }
    }
}
