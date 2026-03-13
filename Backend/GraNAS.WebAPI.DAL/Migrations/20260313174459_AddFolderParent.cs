using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.WebAPI.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "table_folders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_folders_parent_id",
                table: "table_folders",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "FK_table_folders_table_folders_parent_id",
                table: "table_folders",
                column: "parent_id",
                principalTable: "table_folders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_table_folders_table_folders_parent_id",
                table: "table_folders");

            migrationBuilder.DropIndex(
                name: "IX_folders_parent_id",
                table: "table_folders");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "table_folders");
        }
    }
}
