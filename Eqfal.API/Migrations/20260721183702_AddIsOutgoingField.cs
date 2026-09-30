using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eqfal.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIsOutgoingField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOutgoing",
                table: "Operations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOutgoing",
                table: "Operations");
        }
    }
}
