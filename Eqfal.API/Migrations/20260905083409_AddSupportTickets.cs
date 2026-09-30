using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eqfal.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SenderNumber",
                table: "Operations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverNumber",
                table: "Operations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[AuditLogs]') IS NULL
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [Action] nvarchar(100) NOT NULL,
        [Details] nvarchar(500) NOT NULL,
        [IpAddress] nvarchar(50) NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
    CREATE INDEX [IX_AuditLogs_UserId_Timestamp] ON [AuditLogs] ([UserId], [Timestamp]);
END

IF OBJECT_ID(N'[LidMappings]') IS NULL
BEGIN
    CREATE TABLE [LidMappings] (
        [Id] int NOT NULL IDENTITY,
        [Lid] nvarchar(50) NOT NULL,
        [RealPhone] nvarchar(50) NOT NULL,
        [ContactName] nvarchar(150) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LidMappings] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_LidMappings_Lid] ON [LidMappings] ([Lid]);
END

IF OBJECT_ID(N'[SystemSettings]') IS NULL
BEGIN
    CREATE TABLE [SystemSettings] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(max) NOT NULL,
        [Value] nvarchar(max) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Id])
    );
END

IF OBJECT_ID(N'[SupportTickets]') IS NULL
BEGIN
    CREATE TABLE [SupportTickets] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [FullName] nvarchar(150) NOT NULL,
        [PhoneNumber] nvarchar(30) NOT NULL,
        [Email] nvarchar(150) NULL,
        [Subject] nvarchar(200) NULL,
        [Message] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_SupportTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupportTickets_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
    CREATE INDEX [IX_SupportTickets_UserId] ON [SupportTickets] ([UserId]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "LidMappings");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.AlterColumn<string>(
                name: "SenderNumber",
                table: "Operations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverNumber",
                table: "Operations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
