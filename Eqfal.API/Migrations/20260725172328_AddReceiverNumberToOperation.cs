using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eqfal.API.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiverNumberToOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiverNumber",
                table: "Operations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiverNumber",
                table: "Operations");
        }
    }
}
