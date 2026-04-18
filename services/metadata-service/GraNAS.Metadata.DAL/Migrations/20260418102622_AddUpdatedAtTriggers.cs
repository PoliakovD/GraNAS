using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Metadata.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtTriggers : Migration
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
                $$ language 'plpgsql';
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS update_folders_updated_at ON table_folders;
                CREATE TRIGGER update_folders_updated_at
                    BEFORE UPDATE ON table_folders
                    FOR EACH ROW
                    EXECUTE FUNCTION update_updated_at_column();
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS update_files_updated_at ON table_files;
                CREATE TRIGGER update_files_updated_at
                    BEFORE UPDATE ON table_files
                    FOR EACH ROW
                    EXECUTE FUNCTION update_updated_at_column();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_folders_updated_at ON table_folders;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_files_updated_at ON table_files;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_updated_at_column();");
        }
    }
}
