using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterMusic.Api.Migrations;

public partial class CorreoUsuarioUnico : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT [email]
                FROM [Usuario]
                GROUP BY [email]
                HAVING COUNT(*) > 1
            )
            BEGIN
                THROW 51000, 'Hay correos duplicados en Usuario. Revisa las cuentas antes de aplicar CorreoUsuarioUnico; la migracion no elimina datos.', 1;
            END;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Usuario_email",
            table: "Usuario",
            column: "email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Usuario_email", table: "Usuario");
    }
}
