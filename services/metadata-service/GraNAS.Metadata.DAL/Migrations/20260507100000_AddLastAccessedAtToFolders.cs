using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Metadata.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddLastAccessedAtToFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_accessed_at",
                table: "table_folders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_folders_last_accessed_at",
                table: "table_folders",
                columns: new[] { "owner_id", "last_accessed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_folders_last_accessed_at",
                table: "table_folders");

            migrationBuilder.DropColumn(
                name: "last_accessed_at",
                table: "table_folders");
        }
    }
}
