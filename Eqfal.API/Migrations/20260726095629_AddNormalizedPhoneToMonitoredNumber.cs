using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eqfal.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedPhoneToMonitoredNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "MonitoredNumbers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "MonitoredNumbers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // ملء NormalizedPhone للسجلات الموجودة مسبقاً:
            // 1. أزل كل الرموز وابقي الأرقام فقط
            // 2. إذا الرقم يبدأ بـ 09 (ليبي محلي) → حوّله لـ 2189...
            migrationBuilder.Sql(@"
                UPDATE MonitoredNumbers
                SET NormalizedPhone = 
                    CASE 
                        WHEN PATINDEX('%[^0-9]%', PhoneNumber) = 0 
                             AND LEFT(PhoneNumber, 2) = '09'
                             AND LEN(PhoneNumber) >= 10
                        THEN '2189' + SUBSTRING(PhoneNumber, 3, LEN(PhoneNumber))
                        ELSE PhoneNumber
                    END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "MonitoredNumbers");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "MonitoredNumbers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);
        }
    }
}
