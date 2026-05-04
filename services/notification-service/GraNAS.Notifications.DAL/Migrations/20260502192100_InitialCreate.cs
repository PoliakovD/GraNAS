using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Notifications.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_updated_at_column()
                RETURNS TRIGGER AS $$
                BEGIN
                    NEW.updated_at = NOW();
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.CreateTable(
                name: "table_notification_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_notification_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "table_notification_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Pending"),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_notification_outbox", x => x.id);
                    table.ForeignKey(
                        name: "FK_table_notification_outbox_table_notification_events_notific~",
                        column: x => x.notification_event_id,
                        principalTable: "table_notification_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_event_id",
                table: "table_notification_events",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_user_created",
                table: "table_notification_events",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_events_user_read",
                table: "table_notification_events",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_outbox_delivery",
                table: "table_notification_outbox",
                columns: new[] { "target", "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_table_notification_outbox_notification_event_id",
                table: "table_notification_outbox",
                column: "notification_event_id");

            migrationBuilder.Sql(@"
                CREATE INDEX ix_notification_events_data_gin
                ON table_notification_events USING GIN (data);
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS update_notification_outbox_updated_at ON table_notification_outbox;
                CREATE TRIGGER update_notification_outbox_updated_at
                    BEFORE UPDATE ON table_notification_outbox
                    FOR EACH ROW
                    EXECUTE FUNCTION update_updated_at_column();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_notification_outbox_updated_at ON table_notification_outbox;");
            migrationBuilder.DropTable(name: "table_notification_outbox");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_notification_events_data_gin;");
            migrationBuilder.DropTable(name: "table_notification_events");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_updated_at_column();");
        }
    }
}
