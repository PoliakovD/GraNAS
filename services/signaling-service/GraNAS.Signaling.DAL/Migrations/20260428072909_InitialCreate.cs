using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Signaling.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "table_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_devices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_table_devices_user_id",
                table: "table_devices",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_table_devices_user_id_device_name",
                table: "table_devices",
                columns: new[] { "user_id", "device_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "table_devices");
        }
    }
}
