using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanzasIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCategoriasSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activa",
                table: "Categorias",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsSistema",
                table: "Categorias",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Activa", "EsSistema" },
                values: new object[] { true, false });

            migrationBuilder.UpdateData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Activa", "EsSistema" },
                values: new object[] { true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activa",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "EsSistema",
                table: "Categorias");
        }
    }
}
