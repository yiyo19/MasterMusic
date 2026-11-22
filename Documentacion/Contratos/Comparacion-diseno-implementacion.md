# Contratos de diseño y contratos de implementación

Fecha: 22 de noviembre de 2026. Versión documental: 1.0.2.

## Origen de los archivos

Los contratos de [catálogo](mastermusic-api.openapi.json) y [pedidos](mastermusic-ventas-api.openapi.json) de esta carpeta conservan el diseño de la semana 2. Su historial acredita que se prepararon antes de implementar los nuevos flujos.

La carpeta Implementacion contiene las exportaciones de las APIs ejecutadas localmente:

| API | Documento generado | Fuente |
| --- | --- | --- |
| Catálogo | [mastermusic-api.openapi.json](Implementacion/mastermusic-api.openapi.json) | https://localhost:7052/swagger/v1/swagger.json |
| Pedidos | [mastermusic-ventas-api.openapi.json](Implementacion/mastermusic-ventas-api.openapi.json) | https://localhost:7267/openapi/v1.json |

Se deben regenerar ambos JSON después de aplicar cambios en los metadatos Swagger y reiniciar las APIs. Guardar la respuesta HTTP sin editarla permite conservar el contrato producido por el código. El documento generado describe la implementación; no reemplaza ni cambia retroactivamente el diseño original.

## Comparación de rutas y permisos

La comparación de las rutas exportadas con el diseño encuentra las mismas 15 rutas del catálogo y 9 rutas de pedidos. Para compararlas se sustituye {version} por 1.0 en el catálogo. Ese parámetro pertenece al segmento de la URL y el controlador admite la versión 1.0. La versión del formato OpenAPI es distinta de la versión de la API.

| Aspecto | Diseño | Implementación |
| --- | --- | --- |
| Identidad | Registro y login públicos | Se conservan ambas rutas. Registro asigna Cliente; login emite JWT. |
| Catálogo público | GET de instrumentos activos | Se conserva; incluye nombres de categoría y marca. |
| Administración | Instrumentos, categorías y marcas | Las rutas administrativas requieren Administrador. |
| Integración | JWT y clave de servicio | Las rutas internas requieren simultáneamente Bearer y X-Service-Key. |
| Pedidos propios | Cliente identificado por JWT | La cuenta consulta y crea sus propios pedidos; no elige otro cliente en el cuerpo. |
| Administración de pedidos | Confirmar y cancelar | Confirmar no vuelve a descontar; cancelar verifica la devolución antes de finalizar. |
| Operaciones | Consulta y recuperación por propietario | El creador consulta con su sesión y recupera con el rol correspondiente. |

La autenticación y autorización se aplican en los controladores y filtros. Los metadatos Swagger describen esas reglas; no son el mecanismo que impide un acceso.

## Respuestas y esquemas

El ajuste documental 1.0.2 declara los esquemas de respuesta y distingue:

- Registro y login: 200; correo repetido: 409; credenciales rechazadas: 401.
- Creación de instrumento, categoría o marca: 201; edición y eliminación correctas: 204, sin cuerpo.
- Descuento o devolución de inventario: 201 al aplicarse; 200 al recuperar la misma operación.
- Creación de pedido: 201 con el pedido; reintento completado: 200 con el mismo pedido; resultado incierto: 202 con la operación recuperable.
- Confirmación o cancelación: 200 con el pedido al completarse, o 202 mientras la operación requiere recuperación.
- Recuperación: 200 con la operación al alcanzar un resultado terminal, o 202 mientras continúa siendo recuperable.
- Respuestas 201 y 202: cabecera Location; en 202, también Retry-After.

PedidoRespuesta, OperacionRespuesta y MovimientoRespuesta son nombres del código. Cumplen funciones equivalentes a Pedido, Operacion y Movimiento del diseño; no se debe comparar únicamente el nombre del esquema. Las respuestas de acceso contienen datos públicos, nunca el hash de contraseña.

## Diferencias conservadas y limitaciones

| Tema | Diferencia frente al diseño |
| --- | --- |
| Entrada de instrumentos | Los controladores existentes reciben Instrumento, en lugar de los DTO separados InstrumentoCrear e InstrumentoEditar diseñados. Al crear se descartan ID, estado y relaciones suministrados; el repositorio controla los campos de edición. La respuesta pública utiliza InstrumentoRespuesta. El esquema generado refleja esta entrada real. |
| Errores heredados | Algunas validaciones y respuestas 404 del controlador de instrumentos usan application/json con message. Las nuevas operaciones usan Problem Details y codigo. El cliente interpreta ambos formatos. |
| Códigos de estado del negocio | La transición no permitida de un pedido se identifica con ESTADO_PEDIDO_INVALIDO; el diseño utilizaba ESTADO_INVALIDO. El estado HTTP continúa siendo 409. |
| Compensación automática | El diseño contemplaba EnCompensacion y Compensada. El servicio implementado utiliza recuperación explícita de operaciones inciertas; no ejecuta una compensación automática de altas. La cancelación administrativa sí devuelve stock y está implementada. |
| Restricciones descritas | Las reglas que comparan recursos, propietarios, precios, stock y total se validan en controladores, servicios y SQL. No todas pueden representarse mediante anotaciones de un único esquema JSON. |

No se presenta la implementación como idéntica al diseño en estos puntos. Los cambios quedan registrados en [CAMBIOS.md](CAMBIOS.md), y las limitaciones deben mantenerse también en el informe.

## Verificación reproducible

Las pruebas de Swagger comprueban acceso público frente a JWT, el requisito combinado de credenciales internas, los esquemas de respuesta sin contraseña, y la diferencia entre pedido completado y operación recuperable.

Desde la raíz del repositorio:

~~~powershell
dotnet test .\MasterMusic.Tests\MasterMusic.Tests.csproj --filter "FullyQualifiedName~Swagger" --logger "trx;LogFileName=contratos.trx" --results-directory .\TestResults\Contratos
~~~

Esta selección contiene 11 casos. La suite completa contiene 103 casos .NET, incluidos 50 con SQL Server. El número de casos definidos no acredita por sí mismo una ejecución: los resultados se consultan en los TRX y en Actions del commit correspondiente. Las pruebas de Swagger no sustituyen las de autorización, persistencia, concurrencia ni los flujos reales del cliente.

La colección manual está en [MasterMusic.http](../Pruebas/MasterMusic.http). Las variables con credenciales se completan en una copia local excluida de Git.
