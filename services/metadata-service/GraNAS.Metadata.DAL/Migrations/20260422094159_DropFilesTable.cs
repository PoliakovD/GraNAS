using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Metadata.DAL.Migrations
{
    /// <inheritdoc />
    public partial class DropFilesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "table_files");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "table_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_table_files_table_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "table_folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_files_folder_id",
                table: "table_files",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_files_owner_id",
                table: "table_files",
                column: "owner_id");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS update_files_updated_at ON table_files;
                CREATE TRIGGER update_files_updated_at
                    BEFORE UPDATE ON table_files
                    FOR EACH ROW
                    EXECUTE FUNCTION update_updated_at_column();
            ");
        }
    }
}
