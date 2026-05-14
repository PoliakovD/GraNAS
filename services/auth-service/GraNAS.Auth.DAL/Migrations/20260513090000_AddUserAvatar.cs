using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Auth.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "avatar",
                table: "table_users",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "avatar_content_type",
                table: "table_users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "avatar_updated_at",
                table: "table_users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "avatar", table: "table_users");
            migrationBuilder.DropColumn(name: "avatar_content_type", table: "table_users");
            migrationBuilder.DropColumn(name: "avatar_updated_at", table: "table_users");
        }
    }
}
