using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterMusic.Api.Migrations
{
    /// <inheritdoc />
    public partial class InventarioPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "version",
                table: "Instrumento",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "MovimientoInventario",
                columns: table => new
                {
                    id_operacion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    id_pedido = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    id_cliente = table.Column<int>(type: "int", nullable: false),
                    tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    huella_solicitud = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    id_operacion_origen = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientoInventario", x => x.id_operacion);
                    table.ForeignKey(
                        name: "FK_MovimientoInventario_MovimientoInventario_id_operacion_origen",
                        column: x => x.id_operacion_origen,
                        principalTable: "MovimientoInventario",
                        principalColumn: "id_operacion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetalleMovimientoInventario",
                columns: table => new
                {
                    id_detalle_movimiento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_operacion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    id_instrumento = table.Column<int>(type: "int", nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    nombre_instrumento = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    precio_unitario = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetalleMovimientoInventario", x => x.id_detalle_movimiento);
                    table.CheckConstraint("CK_Movimiento_Cantidad", "[cantidad] > 0");
                    table.CheckConstraint("CK_Movimiento_Precio", "[precio_unitario] > 0");
                    table.ForeignKey(
                        name: "FK_DetalleMovimientoInventario_Instrumento_id_instrumento",
                        column: x => x.id_instrumento,
                        principalTable: "Instrumento",
                        principalColumn: "id_instrumento",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetalleMovimientoInventario_MovimientoInventario_id_operacion",
                        column: x => x.id_operacion,
                        principalTable: "MovimientoInventario",
                        principalColumn: "id_operacion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instrumento_Stock",
                table: "Instrumento",
                sql: "[stock] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleMovimientoInventario_id_instrumento",
                table: "DetalleMovimientoInventario",
                column: "id_instrumento");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleMovimientoInventario_id_operacion_id_instrumento",
                table: "DetalleMovimientoInventario",
                columns: new[] { "id_operacion", "id_instrumento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_id_operacion_origen",
                table: "MovimientoInventario",
                column: "id_operacion_origen",
                unique: true,
                filter: "[id_operacion_origen] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_id_pedido",
                table: "MovimientoInventario",
                column: "id_pedido",
                unique: true,
                filter: "[tipo] = N'Descuento'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetalleMovimientoInventario");

            migrationBuilder.DropTable(
                name: "MovimientoInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instrumento_Stock",
                table: "Instrumento");

            migrationBuilder.DropColumn(
                name: "version",
                table: "Instrumento");
        }
    }
}
