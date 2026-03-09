using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionsIngest.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    TransactionId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CardLast4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    LocationCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TransactionTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRevoked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFinalized = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.TransactionId);
                });

            migrationBuilder.CreateTable(
                name: "TransactionRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ChangeType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionRevisions_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRevisions_ChangedAtUtc",
                table: "TransactionRevisions",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRevisions_TransactionId",
                table: "TransactionRevisions",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IsFinalized",
                table: "Transactions",
                column: "IsFinalized");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IsRevoked",
                table: "Transactions",
                column: "IsRevoked");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionTimeUtc",
                table: "Transactions",
                column: "TransactionTimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionRevisions");

            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
