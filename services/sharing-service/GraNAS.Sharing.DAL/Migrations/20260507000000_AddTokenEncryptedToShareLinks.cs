using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Sharing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenEncryptedToShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "token_encrypted",
                table: "table_share_links",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "token_encrypted",
                table: "table_share_links");
        }
    }
}
