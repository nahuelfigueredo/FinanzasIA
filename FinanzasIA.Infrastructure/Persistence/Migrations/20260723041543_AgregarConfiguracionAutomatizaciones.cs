using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinanzasIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionAutomatizaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionesAutomatizacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RegistroAutomaticoWhatsapp = table.Column<bool>(type: "boolean", nullable: false),
                    OcrAutomatico = table.Column<bool>(type: "boolean", nullable: false),
                    ClasificacionAutomatica = table.Column<bool>(type: "boolean", nullable: false),
                    RespuestasAutomaticas = table.Column<bool>(type: "boolean", nullable: false),
                    Recordatorios = table.Column<bool>(type: "boolean", nullable: false),
                    AlertasPresupuesto = table.Column<bool>(type: "boolean", nullable: false),
                    ResumenDiario = table.Column<bool>(type: "boolean", nullable: false),
                    ResumenSemanal = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesAutomatizacion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesAutomatizacion_UsuarioId",
                table: "ConfiguracionesAutomatizacion",
                column: "UsuarioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesAutomatizacion");
        }
    }
}
