using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eqfal.API.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppAuthStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppAuthStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    KeyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    JsonData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppAuthStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppAuthStates_UserId_KeyId",
                table: "WhatsAppAuthStates",
                columns: new[] { "UserId", "KeyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppAuthStates");
        }
    }
}
