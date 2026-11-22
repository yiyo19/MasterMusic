using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterMusic.Ventas.Api.Migrations
{
    /// <inheritdoc />
    public partial class InicialPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pedido",
                columns: table => new
                {
                    id_pedido = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    id_cliente = table.Column<int>(type: "int", nullable: false),
                    fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    total = table.Column<long>(type: "bigint", nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedido", x => x.id_pedido);
                    table.CheckConstraint("CK_Pedido_Estado", "[estado] IN (N'Pendiente', N'Confirmado', N'Cancelado')");
                    table.CheckConstraint("CK_Pedido_MotivoCancelacion", "([estado] = N'Cancelado' AND [motivo_cancelacion] IS NOT NULL AND LEN([motivo_cancelacion]) > 0) OR ([estado] <> N'Cancelado' AND [motivo_cancelacion] IS NULL)");
                    table.CheckConstraint("CK_Pedido_Total", "[total] BETWEEN 1 AND 9007199254740991");
                });

            migrationBuilder.CreateTable(
                name: "DetallePedido",
                columns: table => new
                {
                    id_detalle_pedido = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_pedido = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    id_instrumento = table.Column<int>(type: "int", nullable: false),
                    nombre_instrumento = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    precio_unitario = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallePedido", x => x.id_detalle_pedido);
                    table.CheckConstraint("CK_DetallePedido_Cantidad", "[cantidad] > 0");
                    table.CheckConstraint("CK_DetallePedido_PrecioUnitario", "[precio_unitario] > 0");
                    table.ForeignKey(
                        name: "FK_DetallePedido_Pedido_id_pedido",
                        column: x => x.id_pedido,
                        principalTable: "Pedido",
                        principalColumn: "id_pedido",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedido_id_pedido_id_instrumento",
                table: "DetallePedido",
                columns: new[] { "id_pedido", "id_instrumento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_id_cliente_fecha",
                table: "Pedido",
                columns: new[] { "id_cliente", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallePedido");

            migrationBuilder.DropTable(
                name: "Pedido");
        }
    }
}
