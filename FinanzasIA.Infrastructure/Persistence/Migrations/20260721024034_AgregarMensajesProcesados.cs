using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinanzasIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMensajesProcesados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MensajesProcesados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    Intent = table.Column<int>(type: "integer", nullable: false),
                    Exito = table.Column<bool>(type: "boolean", nullable: false),
                    MovimientoId = table.Column<int>(type: "integer", nullable: true),
                    Respuesta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DuracionMs = table.Column<long>(type: "bigint", nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesProcesados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesProcesados_FechaCreacion",
                table: "MensajesProcesados",
                column: "FechaCreacion");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesProcesados_UsuarioId",
                table: "MensajesProcesados",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensajesProcesados");
        }
    }
}
