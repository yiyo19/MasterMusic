using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterMusic.Ventas.Api.Migrations
{
    /// <inheritdoc />
    public partial class OperacionesPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperacionPedido",
                columns: table => new
                {
                    id_operacion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    clave_cliente = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    id_pedido = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    estado_operacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    huella_solicitud = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    solicitud_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_actualizacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    codigo_resultado = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    problema_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionPedido", x => x.id_operacion);
                    table.ForeignKey(
                        name: "FK_OperacionPedido_Pedido_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "Pedido",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperacionPedido_id_pedido",
                table: "OperacionPedido",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "IX_OperacionPedido_id_usuario_tipo_clave_cliente",
                table: "OperacionPedido",
                columns: new[] { "id_usuario", "tipo", "clave_cliente" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperacionPedido");
        }
    }
}
