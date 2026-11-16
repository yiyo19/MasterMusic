# Contratos OpenAPI — MasterMusic, Trabajo 3

Versión inicial de diseño: 1.0.0 — 10 de noviembre de 2026.

Estos dos archivos JSON son contratos OpenAPI 3.0.3 importables en herramientas compatibles. Cubren el contrato de ambas APIs solicitado en el Hito 2 (semana 2). Describen el comportamiento objetivo; no prueban que los endpoints nuevos estén implementados. La especificación utilizada es https://spec.openapis.org/oas/v3.0.3.html.

| Archivo | Responsabilidad |
|---|---|
| mastermusic-api.openapi.json | Identidad, catálogo, administración e inventario interno; MasterMusicDB. |
| mastermusic-ventas-api.openapi.json | Pedidos y operaciones recuperables; MasterMusicVentasDB. |
| CAMBIOS.md | Origen, versiones y diferencias frente al código del Trabajo 2. |

Cada contrato incluye rutas, verbos, cuerpos, respuestas, esquemas, seguridad y referencias RF mediante `x-requisitos`. `x-roles` documenta los roles autorizados: ASP.NET Core debe aplicarlos; OpenAPI por sí solo no ejecuta autorización.

## Uso

Importar cada JSON por separado en Swagger Editor o una herramienta OpenAPI compatible. El servidor relativo `/` representa el origen de esa API: configurar el host HTTPS y puerto real al ejecutar. No apunta a una instalación publicada ni fija puertos que todavía no se han configurado. No introducir credenciales reales en los archivos.

Para el cliente React, los JSON de respuesta usan camelCase. Los instrumentos incluyen nombreCategoria y nombreMarca; el catálogo público no necesita permisos administrativos para mostrar esos nombres. Los opcionales de edición completa omitidos pasan a null. Stock y estado se modifican en endpoints separados. La entrada administrativa de existencias suma atómicamente, pero no promete idempotencia: no reintentar automáticamente un POST de entrada cuya respuesta se perdió.

Los precios son enteros CLP. Pedido.total usa int64; se limita a 9007199254740991 para que React lo represente exactamente. Validar ese límite antes de descontar inventario; la base conserva bigint. Es una precisión técnica del contrato, no un cambio a pagos o alcance.

## Seguridad y errores

- Registro y login son públicos. El registro solo asigna Cliente. No se agrega una política de complejidad de contraseña.
- Catálogo público solo muestra activos, incluyendo agotados. Administración requiere Administrador.
- JWT válido en las dos APIs: firma, emisor, audiencia y expiración. Cliente solo consulta sus pedidos. Se responde 404 para pedidos u operaciones ajenas, sin revelar su existencia.
- Las rutas internas de inventario requieren **JWT y X-Service-Key simultáneamente**. El servicio de pedidos obtiene la credencial de variables de entorno; React nunca la recibe. La API de inventario verifica además propietario/rol. No guardar JWT ni credencial en OperacionPedido.
- La recuperación manual necesita una sesión vigente del creador. Si expiró, iniciar sesión nuevamente y retomar el mismo identificador; no se promete recuperación automática en segundo plano con tokens almacenados.
- Logout elimina la sesión en React; no existe endpoint de revocación en este alcance. 401 en rutas protegidas exige nueva sesión; 403 conserva la sesión y comunica falta de permiso.
- Los errores objetivo usan application/problem+json: type, title, status, detail y codigo. Pueden incluir traceId y errors por campo. No incluir excepciones, contraseñas ni hashes.

| HTTP | Código de ejemplo | Significado |
|---|---|---|
| 400 | VALIDACION, REFERENCIA_INVALIDA, TOTAL_FUERA_DE_RANGO | Entrada inválida, FK indicada inexistente o total fuera del rango admitido. |
| 401 | CREDENCIALES_INVALIDAS, NO_AUTENTICADO | Login rechazado o credencial ausente/inválida/expirada. |
| 403 | PROHIBIDO | Identidad válida sin permiso para la operación. |
| 404 | NO_ENCONTRADO | Recurso inexistente o no visible. |
| 409 | CORREO_DUPLICADO | Correo ya registrado. |
| 409 | PRECIO_CAMBIADO, STOCK_INSUFICIENTE, INSTRUMENTO_INACTIVO | Revisar selección; no se descuenta parcialmente. PRECIO_CAMBIADO incluye preciosActualizados. |
| 409 | IDEMPOTENCIA_CONFLICTO, ESTADO_INVALIDO, OPERACION_EN_CURSO | Comando diferente con misma clave, transición inválida o conflicto entre operaciones distintas. |
| 409 | RECURSO_EN_USO, STOCK_NO_CERO, STOCK_FUERA_DE_RANGO | No puede borrar historial o aplicar cantidad fuera del rango del stock. |
| 503 | SERVICIO_NO_DISPONIBLE | No se pudo ofrecer respuesta fiable; no asumir ausencia de efectos. |

Estos códigos deberán mantenerse estables en la implementación. En 401 de credenciales de servicio, no revelar cuál credencial interna falló.

## Pedidos y recuperación

1. React genera un UUID en Idempotency-Key por comando y conserva la misma clave durante sus reintentos. La unicidad es usuario + tipo + clave; la huella incluye el pedido de destino y todo el contenido relevante. El orden de líneas se canoniza. No permite instrumentos duplicados.
2. Ventas persiste el comando y genera idOperacion e idPedido antes del HTTP. idPedido previsto queda en el comando mientras la FK de OperacionPedido puede ser null. Los identificadores internos no los elige React.
3. Inventario valida todas las líneas, descuenta y guarda el movimiento en una transacción local. Índices únicos, actualización condicional y restricciones impiden doble descuento y stock negativo. Devuelve nombres y precios históricos.
4. Ventas guarda pedido y detalles y finaliza operación en una transacción local. POST Pedidos devuelve 201; un reintento completado devuelve 200 con el mismo pedido. Pendiente significa pedido aceptado, no operación técnica incompleta.
5. Una operación persistida todavía sin resultado final devuelve 202 con Operacion, Location y Retry-After. Un timeout no prueba rechazo. React conserva la selección y clave, e informa que se verifica el resultado.
6. Si no recibió ni el identificador, React consulta GET Operaciones con claveCliente y tipo. Con idOperacion usa GET para estado y POST recuperacion para reanudar. GET no modifica inventario. 404 permite reenviar el comando con la misma clave; nunca crear otro por un timeout.
7. Cada HTTP interno tiene timeout de cinco segundos mediante cliente tipado e IHttpClientFactory. Una dependencia caída con operación durable conocida devuelve 202 recuperable. Un 503 o respuesta perdida exige consultar/reintentar con la misma clave: no implica que nada se guardó.
8. Confirmar no modifica stock. Cancelar guarda intención y motivo y excluye confirmación concurrente; solo marca Cancelado tras verificar devolución única. El motivo se conserva en el comando durante procesamiento y en Pedido al finalizar.
9. Recuperar consulta el movimiento original y finaliza el pedido faltante. Si debe compensar un alta imposible de completar, persiste EnCompensacion y clave de devolución antes de HTTP. Solo marca Compensada al confirmar devolución. No finalizar y compensar concurrentemente. Las devoluciones toman cantidades del descuento original, incluso si el instrumento está inactivo.

GET Operaciones puede responder 200 aun cuando la operación siga en proceso: es éxito de la **consulta**. En Operacion, Completada incluye pedido y recuperable=false; Rechazada o Compensada incluyen problema y recuperable=false; EnProceso o EnCompensacion llevan recuperable=true. Un POST de recuperación devuelve 200 para estado terminal y 202 si continúa en proceso. Examinar siempre estadoOperacion. Un comando rechazado conserva su resultado; para una selección corregida se genera una clave nueva.

El pedido devuelto en un reintento es el mismo recurso con su estado actual; no se promete una copia idéntica del JSON si después fue confirmado o cancelado. Los estados finales no admiten edición o eliminación.

No hay una transacción distribuida ni garantía de respuesta instantánea entre servicios. El diseño utiliza transacciones locales, unicidad, comandos persistidos y recuperación explícita.

## Verificación y alcance

Los contratos deben mantenerse sincronizados con arquitectura, ER y secuencias. El modelo ER no necesita nuevas tablas para estas rutas. Los campos nombreCategoria/nombreMarca son proyecciones, no nuevas columnas duplicadas. Las entidades antiguas Venta, DetalleVenta y Resena se conservan sin agregar endpoints fuera de alcance.

La API de pedidos identifica al cliente por idCliente; no duplica su correo ni datos personales. La matriz final requisito → historia → endpoint se completará por separado con las HU reales de la ERS; no se inventan referencias de historias.

La validación estructural de archivos no sustituye pruebas HTTP ni SQL Server. Estos contratos no certifican que React, API 2, recuperación o nuevas migraciones ya funcionen. El resultado anterior de 27 pruebas pertenece a la suite ejecutada, no prueba estos endpoints futuros.
