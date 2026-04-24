using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Metadata.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "table_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_table_permissions_table_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "table_folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_folder_user",
                table: "table_permissions",
                columns: new[] { "folder_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_user_id",
                table: "table_permissions",
                column: "user_id");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS update_permissions_updated_at ON table_permissions;
                CREATE TRIGGER update_permissions_updated_at
                    BEFORE UPDATE ON table_permissions
                    FOR EACH ROW
                    EXECUTE FUNCTION update_updated_at_column();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_permissions_updated_at ON table_permissions;");

            migrationBuilder.DropTable(
                name: "table_permissions");
        }
    }
}
