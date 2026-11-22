# Matriz de trazabilidad de MasterMusic

Relaciona los 22 requisitos funcionales y las 18 historias de la [ERS](ERS-MasterMusic.docx) con las rutas, el cliente y las pruebas de la implementación. Las prioridades de esos requisitos en la ERS son Must. Los requisitos no funcionales se verifican por separado porque una prueba funcional aprobada no demuestra por sí sola rendimiento o disponibilidad.

**Registro actualizado:** 22 de noviembre de 2026.

## Convenciones

- **API 1:** catálogo, autenticación e inventario; `https://localhost:7052`.
- **API 2:** pedidos y operaciones; `https://localhost:7267`.
- Todas las rutas indicadas parten de `/api/v1.0`.
- Las acciones exclusivas del navegador se indican expresamente; no se les atribuye un endpoint inexistente.
- Los nombres de pruebas permiten localizar la verificación; el resultado de ejecución corresponde al reporte TRX del commit probado.

## Acceso y catálogo público

| RF | HU | Comportamiento | Ruta o componente | Verificación |
| --- | --- | --- | --- | --- |
| RF-01 | HU-01 | Registro con datos obligatorios y rol Cliente. | API 1: `POST /Auth/registro`; `Auth.jsx`. | `AuthTests.Registro_UsuarioNuevo_Devuelve200` y `AuthValidacionTests`. El índice único en SQL y `AuthRegistroSqlTests` verifican el correo repetido y las altas simultáneas. |
| RF-02 | HU-02 | Login de cuenta activa y emisión del JWT. | API 1: `POST /Auth/login`; `Auth.jsx`. | `Login_CredencialesCorrectas_DevuelveToken`, `Login_ClaveIncorrecta_Devuelve401` y `Login_UsuarioInactivo_Devuelve401_SinToken`. |
| RF-03 | HU-03 | Salir y dejar de enviar el token. | `App.jsx` y `api.mjs`; acción local, sin endpoint logout. | Flujo de salida en React y prueba de cierre de sesión ante 401 en `client.test.mjs`. La prueba automatizada no sustituye la comprobación visual del botón Salir. |
| RF-04 | HU-04 | Catálogo público de activos, precio y disponibilidad. | API 1: `GET /Instrumentos`; `Catalog.jsx`. | `GetInstrumentos_SinToken_Devuelve200` y `CatalogoPublico_OcultaInactivos_YAdministradorLosConsulta`. |
| RF-05 | HU-04 | Detalle de un instrumento activo y campos sin información. | API 1: `GET /Instrumentos/{id}`; `Catalog.jsx`. | `GetPorId_Existente_Devuelve200`, `GetPorId_Inexistente_Devuelve404` y presentación de «No informado» en el cliente. |

## Administración del catálogo

| RF | HU | Comportamiento | Ruta de API 1 | Verificación |
| --- | --- | --- | --- | --- |
| RF-06 | HU-05 | Consulta de activos e inactivos por administrador. | `GET /admin/Instrumentos` y `/{id}`. | `CatalogoPublico_OcultaInactivos_YAdministradorLosConsulta`. |
| RF-07 | HU-06 | Creación con precio positivo, stock no negativo y referencias válidas. | `POST /Instrumentos`. | `InstrumentosCrudTests`, `InstrumentosValidacionTests` y `InstrumentosReferenciasTests`. |
| RF-08 | HU-07 | Edición de datos sin sobrescribir stock ni historial. | `PUT /Instrumentos/{id}`. | `EditarDatos_NoSobrescribeStockNiEstado` y `CancelarInstrumentoInactivo_UsaCantidadesYPreciosHistoricos`. |
| RF-09 | HU-08 | Activación y desactivación. | `PATCH /Instrumentos/{id}/estado`. | `Estado_AusenteEsInvalido_YActivarPublicaInstrumento` y `InstrumentoInactivo_Devuelve409`. |
| RF-10 | HU-09 | Eliminar solo con stock cero y sin referencias. | `DELETE /Instrumentos/{id}`. | `InstrumentoConStockSinHistorial_RechazaEliminarYConservaDatos`, `InstrumentoSinStockNiHistorial_SeElimina`, `InstrumentoConPedido_NoSeElimina` y `EliminarYSumarStockConcurrentes_NoEliminanExistencias`. |
| RF-11 | HU-10 | CRUD de categorías y protección de referencias. | `GET/POST /Categorias`; `GET/PUT/DELETE /Categorias/{id}`. | `Categoria_CrearEditarConsultarEliminar_SinReferencias` y `EliminarCategoriaOMarcaEnUso_NoBorraInstrumentosEnCascada`. |
| RF-12 | HU-11 | CRUD de marcas y protección de referencias. | `GET/POST /Marcas`; `GET/PUT/DELETE /Marcas/{id}`. | `Marca_CrearEditarConsultarEliminar_SinReferencias` y `EliminarCategoriaOMarcaEnUso_NoBorraInstrumentosEnCascada`. |
| RF-13 | HU-12 | Entradas positivas de stock sin perder actualizaciones simultáneas. | `POST /Instrumentos/{id}/existencias`. | `EntradasSimultaneas_SumanSinPerderExistencias`, `EntradaStock_ValidaCantidadYNoDesborda` y `Cliente_NoPuedeCambiarStockNiEstado`. |

React presenta estas operaciones en `Inventory.jsx`. Las rutas de escritura y administración exigen `Administrador`; las pruebas también verifican el rechazo del rol Cliente.

## Pedidos e integración

| RF | HU | Comportamiento | Ruta o componente | Verificación |
| --- | --- | --- | --- | --- |
| RF-14 | HU-13 | Seleccionar, modificar cantidades y retirar instrumentos antes de enviar. | `Cart.jsx` y `cart.mjs`; lectura del catálogo de API 1. | Pruebas de cantidades, repetidos y selección de `client.test.mjs`. No hay escritura en la API al modificar la selección. |
| RF-15 | HU-14 | Mostrar subtotales y total antes de enviar. | `Cart.jsx` y `cart.mjs`; cálculo local del resumen. | Prueba «evita totales fuera del rango exacto de JavaScript» y comprobación del resumen en React. |
| RF-16 | HU-15 | Validar cuenta, cantidades, productos, precios y stock. | API 2: `POST /Pedidos` → API 1: `POST /interno/Movimientos/descuentos`. | `StockInsuficiente_RechazaTodasLasLineasYConservaResultado`, `PrecioCambiado_DevuelvePrecioVigenteYConservaStock` y `DuplicadosCantidadCeroYCamposAjenos_Devuelven400`. |
| RF-17 | HU-15 | Pedido único Pendiente con identidad y detalles históricos. | API 2: `POST /Pedidos`. | `AltaYReintento_GuardanUnPedidoYUnDescuento`; entidades `Pedido` y `DetallePedido`. |
| RF-18 | HU-16 | Lista y detalle propios, sin acceso a otro cliente. | API 2: `GET /Pedidos` y `/Pedidos/{idPedido}`. | `PedidosConsultaTests` y `ClienteAjeno_Recibe404EnPedidoYOperacion`. |
| RF-19 | HU-17 | Consulta administrativa de todos los pedidos. | API 2: `GET /admin/Pedidos` y `/{idPedido}`. | Pruebas de administración y permisos de `PedidosCambiosSqlTests`; pantalla `Orders.jsx`. |
| RF-20 | HU-18 | Confirmar o cancelar pendientes, con motivo y estados finales. | API 2: `POST /admin/Pedidos/{idPedido}/confirmacion` y `/cancelacion`. | `Finalizados_RechazanOtraTransicionConClaveNueva` y `Cancelar_YReintentar_DevuelveUnaVezYGuardaMotivo`; evidencias 01 y 03. |
| RF-21 | HU-15, HU-18 | Stock coherente, concurrencia e idempotencia. | Creación y cambios en API 2; `/interno/Movimientos/descuentos` y `/devoluciones` en API 1. | `ComprasConcurrentes_NoVendenMasDelStock`, `MismaClaveConcurrente_ConservaUnPedido`, `Confirmar_NoDescuentaOtraVezNiDependeDeInventario` y `CancelacionesConcurrentes_MismaClaveDevuelveUnaVez`; evidencias 02 y 04. |
| RF-22 | HU-15, HU-18 | Recuperar resultados inciertos sin repetir efectos. | API 2: `GET /Operaciones?claveCliente={uuid}&tipo={tipo}`, `GET /Operaciones/{idOperacion}` y `POST /Operaciones/{idOperacion}/recuperacion`. | `RespuestaPerdida_RecuperaPedidoConUnSoloDescuento`, `InventarioCaido_ConservaOperacionYPermiteRecuperar` y `RespuestaPerdida_DevolucionSeRecuperaSinRepetirStock`; pruebas de recuperación de React. |

## Requisitos no funcionales

| RNF | Verificación relacionada | Cobertura y criterio de cierre |
| --- | --- | --- |
| RNF-01 · JWT y roles | Configuración de ambas APIs; `PedidosConsultaTests`, `InstrumentosTests` y pruebas SQL de permisos. | Se prueban tokens inválidos y permisos. Completar la evidencia de ejecución de la suite del commit final. |
| RNF-02 · Contraseñas | `AuthController`, `SeedData` y respuestas DTO de login. | BCrypt almacena hashes; las respuestas públicas de autenticación no incluyen contraseña ni hash. La revisión del código y los registros complementa las pruebas. |
| RNF-03 · Secretos | Variables de entorno de ambas APIs, [plantilla](../.env.example) y [README](../README.md). | Configuración fuera del código de ejecución. El cierre requiere revisar también el historial Git; las credenciales ficticias del contenedor CI son distintas de las del entorno de trabajo. |
| RNF-04 · Rendimiento | `GET /Instrumentos` con 100 instrumentos activos, 5 consultas de calentamiento y 100 medidas. | La ERS exige al menos 95 respuestas correctas en un máximo de 2 segundos. Se requiere el reporte de esa medición; el tiempo de compilación o de la suite no demuestra esta meta. |
| RNF-05 · Disponibilidad | Lectura de cada API cada 30 segundos durante 30 minutos. | Se requiere registrar 60 consultas por API y ausencia de reinicios. Las capturas de Swagger muestran funcionamiento puntual, no disponibilidad sostenida. |
| RNF-06 · Fallos entre APIs | `InventarioClient`, timeout de 5 segundos y pruebas de pérdida de respuesta. | Hay fallos simulados y recuperación idempotente. El límite se aplica a cada llamada HTTP, no al tiempo total de toda la operación. |
| RNF-07 · Usabilidad | `Auth.jsx`, `Catalog.jsx`, `Inventory.jsx`, `Orders.jsx` y `operations.mjs`. | Estados de carga, errores en español y bloqueo del doble envío. La comprobación visual de cada formulario complementa las pruebas de lógica. |
| RNF-08 · Sesión | `api.mjs` y `App.jsx`. | `client.test.mjs` verifica que 401 cierra sesión y 403 la conserva; la solicitud incierta permanece asociada a su cuenta. |

## Ajustes de cierre y alcance de la verificación

RF-01 incorpora un índice único de correo, la migración `CorreoUsuarioUnico` y la respuesta `409 CORREO_DUPLICADO` tanto en la comprobación previa como ante una colisión al insertar. Las pruebas verifican altas simultáneas, la restricción SQL y la coherencia del modelo con su snapshot. La migración se detiene si ya existen correos duplicados, sin eliminar cuentas.

El registro duplicado y el login rechazado utilizan Problem Details. Parte de los errores heredados de instrumentos todavía utiliza `message`; la comparación de los contratos de diseño con los exportados debe registrar esa diferencia.

RF-10 incorpora en este ajuste el rechazo `409 STOCK_NO_CERO`, además del control de referencias, dentro de la transacción que bloquea el instrumento. La suite queda con **99 casos .NET, 50 de ellos SQL**, más **17 del cliente**. La cantidad de casos no equivale a un resultado aprobado: conservar el TRX de la ejecución final.

## Archivos de prueba y evidencias

- [Autenticación](../MasterMusic.Tests/AuthTests.cs), [validaciones](../MasterMusic.Tests/AuthValidacionTests.cs), [registro concurrente](../MasterMusic.Tests/AuthRegistroSqlTests.cs) y [modelo de migraciones](../MasterMusic.Tests/ModeloCatalogoTests.cs).
- [CRUD de instrumentos](../MasterMusic.Tests/InstrumentosCrudTests.cs), [validaciones](../MasterMusic.Tests/InstrumentosValidacionTests.cs), [referencias](../MasterMusic.Tests/InstrumentosReferenciasTests.cs) y [roles](../MasterMusic.Tests/InstrumentosTests.cs).
- [Catálogo e inventario sobre SQL](../MasterMusic.Tests/CatalogoReactSqlTests.cs).
- [Consulta de pedidos](../MasterMusic.Tests/PedidosConsultaTests.cs) y [creación e integración](../MasterMusic.Tests/PedidosCreacionSqlTests.cs).
- [Cambios de estado](../MasterMusic.Tests/PedidosCambiosSqlTests.cs), [inventario](../MasterMusic.Tests/PedidosCambiosSqlTests.Inventario.cs) y [recuperación](../MasterMusic.Tests/PedidosCambiosSqlTests.Recuperacion.cs).
- [Swagger de pedidos](../MasterMusic.Tests/SwaggerPedidosTests.cs) y [pruebas de React](../MasterMusic.Client/tests/client.test.mjs).
- [Capturas funcionales de semana 3](Evidencias/Semana-3).
