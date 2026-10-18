using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tanningTribe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPostage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PostageAmount",
                table: "Orders",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PostageAmount",
                table: "Orders");
        }
    }
}
