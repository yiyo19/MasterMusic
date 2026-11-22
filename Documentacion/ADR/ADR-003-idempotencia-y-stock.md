# ADR 003 Persistir operaciones para mantener la coherencia del stock

**Estado:** aplicado. **Fecha del registro:** 22 de noviembre de 2026.
**Requisitos relacionados:** RF-10, RF-13, RF-16, RF-17, RF-20 a RF-22 y RNF-06.

## Contexto

Un pedido puede descontar stock correctamente y perder su respuesta por una interrupción. Repetir el descuento sin reconocer la operación anterior duplicaría el efecto. La confirmación y la cancelación también pueden competir por el mismo pedido.

## Decisión

React asigna una `Idempotency-Key` a cada nueva creación, confirmación o cancelación y conserva esa clave al reintentar. La API de pedidos persiste el comando, el actor, el tipo, su huella y un identificador de operación generado por el servidor.

El catálogo registra el movimiento de inventario y sus detalles en la misma transacción que modifica existencias. Una devolución identifica el descuento de origen y no puede devolverlo más de una vez. Se utilizan transacciones, restricciones e identificadores únicos para coordinar reintentos y concurrencia.

El estado técnico se almacena separado del pedido. Los flujos implementados producen `EnProceso`, `Completada` y `Rechazada`. Ante incertidumbre se consulta el movimiento original y se recupera el resultado; no se inventa un éxito ni se inicia otro descuento con otra clave.

El pedido aceptado comienza `Pendiente`. Confirmar no toca stock. Cancelar solicita o recupera la devolución y, cuando esta se confirma, cambia a `Cancelado`. Un pedido finalizado rechaza nuevas transiciones con claves distintas.

La eliminación de instrumentos y las entradas administrativas bloquean la fila durante la transacción. La eliminación comprueba stock cero y ausencia de referencias después de obtener ese bloqueo, para no borrar existencias que otra operación acaba de ingresar.

## Alternativas consideradas

Reintentar escrituras sin persistir identidad puede duplicar movimientos. Mantener todo el progreso solo en memoria pierde la recuperación al reiniciar. Una transacción distribuida entre ambas bases introduce coordinación adicional que no forma parte del despliegue del proyecto.

## Consecuencias

El cliente debe conservar y reutilizar la clave de una solicitud incierta; mientras se resuelve, bloquea otro envío. El servidor compara el contenido para rechazar reutilizar la misma clave con datos distintos.

La disponibilidad de inventario condiciona la creación y la cancelación, pero confirmar un pedido existente no requiere otro movimiento. El límite de cinco segundos corresponde a cada llamada HTTP.

El diseño inicial contempló compensación automática de un alta que no pudiera finalizar. El código actual recupera y completa el alta desde su comando y movimiento persistidos; no implementa un proceso automático que transite a `EnCompensacion` y `Compensada`. Esa alternativa no se presenta como funcionalidad terminada.

## Implementación y verificación

- [Creación y recuperación](../../MasterMusic.Ventas.Api/Services/CreacionPedidosService.cs).
- [Confirmación y cancelación](../../MasterMusic.Ventas.Api/Services/CambiosPedidosService.cs).
- [Bloqueos del catálogo](../../MasterMusic.Api/Data/CatalogoTransaccion.cs).
- [Pruebas de concurrencia y respuesta perdida](../../MasterMusic.Tests/PedidosCreacionSqlTests.cs).
- [Pruebas de recuperación de cambios](../../MasterMusic.Tests/PedidosCambiosSqlTests.Recuperacion.cs).
- [Pruebas de inventario y eliminación](../../MasterMusic.Tests/CatalogoReactSqlTests.cs).
- [Estados del pedido](../Diagrama-estados-pedido-MasterMusic.md).
