using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Sharing.DAL.Migrations
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
                name: "table_share_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_share_links", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_share_links_token_hash",
                table: "table_share_links",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_share_links_folder_id",
                table: "table_share_links",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_share_links_owner_id",
                table: "table_share_links",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_share_links_expires_at",
                table: "table_share_links",
                column: "expires_at");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS update_share_links_updated_at ON table_share_links;
                CREATE TRIGGER update_share_links_updated_at
                    BEFORE UPDATE ON table_share_links
                    FOR EACH ROW
                    EXECUTE FUNCTION update_updated_at_column();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_share_links_updated_at ON table_share_links;");
            migrationBuilder.DropTable(name: "table_share_links");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_updated_at_column();");
        }
    }
}
