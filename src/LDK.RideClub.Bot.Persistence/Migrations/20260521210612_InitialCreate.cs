using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LDK.RideClub.Bot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EventId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SenderId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PayloadType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RawPayload = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_ConversationId",
                table: "MessageLogs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_EventId",
                table: "MessageLogs",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_Platform_ReceivedAt",
                table: "MessageLogs",
                columns: new[] { "Platform", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageLogs");
        }
    }
}
