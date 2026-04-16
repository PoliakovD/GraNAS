using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GraNAS.Auth.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeAndTriggers : Migration
    {
      protected override void Up(MigrationBuilder migrationBuilder)
      {
        // Существующий код создания индексов и внешних ключей (если миграция новая)
        // Если это новая миграция после создания таблиц, то здесь уже будут команды создания.
        // Если таблицы уже созданы, то просто добавляем триггеры.

        // Функция для обновления updated_at
        migrationBuilder.Sql(@"
        CREATE OR REPLACE FUNCTION update_updated_at_column()
        RETURNS TRIGGER AS $$
        BEGIN
            NEW.updated_at = NOW();
            RETURN NEW;
        END;
        $$ language 'plpgsql';
    ");

        // Триггер для таблицы folders
        migrationBuilder.Sql(@"
        DROP TRIGGER IF EXISTS update_folders_updated_at ON table_folders;
        CREATE TRIGGER update_folders_updated_at
            BEFORE UPDATE ON table_folders
            FOR EACH ROW
            EXECUTE FUNCTION update_updated_at_column();
    ");

        // Триггер для таблицы files
        migrationBuilder.Sql(@"
        DROP TRIGGER IF EXISTS update_files_updated_at ON table_files;
        CREATE TRIGGER update_files_updated_at
            BEFORE UPDATE ON table_files
            FOR EACH ROW
            EXECUTE FUNCTION update_updated_at_column();
    ");
      }

      protected override void Down(MigrationBuilder migrationBuilder)
      {
        // Удаление триггеров при откате
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_folders_updated_at ON table_folders;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS update_files_updated_at ON table_files;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_updated_at_column();");
      }
    }
}
