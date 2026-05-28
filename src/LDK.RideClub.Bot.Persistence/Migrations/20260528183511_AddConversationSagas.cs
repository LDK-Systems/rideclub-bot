using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LDK.RideClub.Bot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationSagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationSagas",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CurrentState = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SenderId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<uint>(type: "INTEGER", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSagas", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSagas_CorrelationKey",
                table: "ConversationSagas",
                column: "CorrelationKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationSagas");
        }
    }
}
