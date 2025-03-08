using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MateoAPI.Migrations
{
    /// <inheritdoc />
    public partial class CreateTableEntrenamiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mateo");

            migrationBuilder.CreateTable(
                name: "entrenamiento",
                schema: "mateo",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    id_usuario = table.Column<string>(type: "text", nullable: false),
                    id_request = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    termino = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_tipo_ejercicio = table.Column<int>(type: "integer", nullable: true),
                    serie = table.Column<short>(type: "smallint", nullable: true),
                    repeticiones = table.Column<short>(type: "smallint", nullable: true),
                    segundos_entrenamiento = table.Column<short>(type: "smallint", nullable: true),
                    segundos_descanso = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrenamiento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entrenamiento_id_request",
                schema: "mateo",
                table: "entrenamiento",
                column: "id_request",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entrenamiento_id_usuario_inicio",
                schema: "mateo",
                table: "entrenamiento",
                columns: new[] { "id_usuario", "inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entrenamiento",
                schema: "mateo");
        }
    }
}
