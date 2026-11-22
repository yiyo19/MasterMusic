# ADR 001 Separar catálogo y pedidos en dos APIs

**Estado:** aplicado. **Fecha del registro:** 22 de noviembre de 2026.
**Requisitos relacionados:** RF-04 a RF-22; dos APIs independientes e integración entre servicios exigidas por Trabajo3-CS.

## Contexto

MasterMusic parte de una API con identidad, catálogo y datos heredados de ventas. El Trabajo 3 agrega pedidos desde React y exige dos APIs independientes. El catálogo debe conservar la autoridad sobre productos y stock, mientras que los pedidos necesitan almacenar sus estados y datos históricos.

## Decisión

`MasterMusic.Api` administra usuarios, roles, instrumentos, categorías, marcas y movimientos de inventario en `MasterMusicDB`. `MasterMusic.Ventas.Api` administra pedidos, detalles y operaciones en `MasterMusicVentasDB`. Cada API tiene su contexto EF Core y sus migraciones.

La API de pedidos accede al catálogo mediante el cliente tipado `InventarioClient` registrado con `IHttpClientFactory`. Utiliza HTTPS, un tiempo de espera de cinco segundos por llamada y autorización de servicio adicional al JWT.

Los identificadores de cliente e instrumento en pedidos son referencias externas. No hay claves foráneas ni consultas entre las dos bases. Cada detalle conserva nombre y precio para que la consulta histórica no dependa del catálogo actual.

## Alternativas consideradas

Una única API facilitaría las transacciones, pero no cubriría la separación exigida y concentraría todas las responsabilidades. Dos APIs escribiendo directamente en las mismas tablas evitarían llamadas HTTP, pero acoplarían el esquema y permitirían modificar el stock fuera del servicio responsable.

## Consecuencias

La separación permite ejecutar y evolucionar los servicios por separado, aunque el alta y la cancelación dependen de inventario. Una transacción SQL no abarca ambas APIs: el estado de cada operación debe persistirse y recuperarse. Las consultas de pedidos ya registrados utilizan su propia base.

El despliegue local requiere dos procesos .NET, React y SQL Server con dos bases. Este proyecto no necesita Docker para su ejecución local.

## Implementación y verificación

- [Contexto del catálogo](../../MasterMusic.Api/Data/MasterMusicContext.cs).
- [Contexto de pedidos](../../MasterMusic.Ventas.Api/Data/MasterMusicVentasContext.cs).
- [Cliente HTTP tipado](../../MasterMusic.Ventas.Api/Services/InventarioClient.cs).
- [Pruebas de creación e integración](../../MasterMusic.Tests/PedidosCreacionSqlTests.cs).
- [Despliegue real](../Diagrama-despliegue-MasterMusic.md).

La solución mantiene las entidades heredadas `Venta`, `DetalleVenta` y `Resena` en la primera base, pero el flujo de pedidos del Trabajo 3 utiliza la segunda API.
