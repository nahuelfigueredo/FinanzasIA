using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanzasIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuitarVerificacionWhatsapp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoVerificacion",
                table: "UsuariosWhatsapp");

            migrationBuilder.DropColumn(
                name: "FechaVerificacion",
                table: "UsuariosWhatsapp");

            migrationBuilder.DropColumn(
                name: "Verificado",
                table: "UsuariosWhatsapp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoVerificacion",
                table: "UsuariosWhatsapp",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVerificacion",
                table: "UsuariosWhatsapp",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Verificado",
                table: "UsuariosWhatsapp",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
