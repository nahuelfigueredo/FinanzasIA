using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinanzasIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUsuariosWhatsapp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsuariosWhatsapp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    NumeroTelefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    Verificado = table.Column<bool>(type: "boolean", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CodigoVerificacion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FechaAlta = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaVerificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechaUltimoUso = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosWhatsapp", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosWhatsapp_NumeroTelefono_Canal",
                table: "UsuariosWhatsapp",
                columns: new[] { "NumeroTelefono", "Canal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosWhatsapp_UsuarioId",
                table: "UsuariosWhatsapp",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsuariosWhatsapp");
        }
    }
}
