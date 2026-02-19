using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Shared.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlaggedAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    FlaggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlaggedAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlaggedDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FlaggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlaggedDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlaggedLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FlaggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlaggedLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Records",
                columns: table => new
                {
                    TransactionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    TimeOfTransaction = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    ReceipientId = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Device = table.Column<string>(type: "text", nullable: false),
                    Isfraud = table.Column<bool>(type: "boolean", nullable: false),
                    FraudReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Records", x => x.TransactionId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlaggedAccounts");

            migrationBuilder.DropTable(
                name: "FlaggedDevices");

            migrationBuilder.DropTable(
                name: "FlaggedLocations");

            migrationBuilder.DropTable(
                name: "Records");
        }
    }
}
