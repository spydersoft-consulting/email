using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Spydersoft.EmailApi.Infrastructure.Data.Entities;

#nullable disable

namespace Spydersoft.EmailApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResendId = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    SenderClientId = table.Column<string>(type: "text", nullable: true),
                    SenderUserId = table.Column<string>(type: "text", nullable: true),
                    FromAddress = table.Column<string>(type: "text", nullable: false),
                    To = table.Column<string>(type: "jsonb", nullable: false),
                    Cc = table.Column<string>(type: "jsonb", nullable: false),
                    Bcc = table.Column<string>(type: "jsonb", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    Attachments = table.Column<List<AttachmentMeta>>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastEventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "email_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResendId = table.Column<string>(type: "text", nullable: false),
                    WebhookId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_events_email_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "email_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_events_MessageId_OccurredAt",
                table: "email_events",
                columns: new[] { "MessageId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_events_WebhookId",
                table: "email_events",
                column: "WebhookId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_CreatedAt",
                table: "email_messages",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_IdempotencyKey_SenderClientId",
                table: "email_messages",
                columns: new[] { "IdempotencyKey", "SenderClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_ResendId",
                table: "email_messages",
                column: "ResendId");

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_SenderClientId_CreatedAt",
                table: "email_messages",
                columns: new[] { "SenderClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_messages_Status",
                table: "email_messages",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_events");

            migrationBuilder.DropTable(
                name: "email_messages");
        }
    }
}
