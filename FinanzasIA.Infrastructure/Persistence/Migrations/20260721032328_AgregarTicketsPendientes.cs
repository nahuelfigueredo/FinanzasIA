using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinanzasIA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTicketsPendientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketsPendientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TextoOcr = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Comercio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CategoriaSugerida = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DatoSolicitado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketsPendientes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketsPendientes_UsuarioId_Activo",
                table: "TicketsPendientes",
                columns: new[] { "UsuarioId", "Activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketsPendientes");
        }
    }
}
