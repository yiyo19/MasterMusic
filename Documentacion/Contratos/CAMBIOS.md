# Registro de cambios de contratos

## 1.0.0 — 2026-11-10 — Diseño del Trabajo 3

Primera versión de los contratos separados de MasterMusic.Api y MasterMusic.Ventas.Api, en OpenAPI 3.0.3. Las rutas están versionadas /api/v1.0. La versión de este documento contractual no implica una nueva versión desplegada.

Origen: API y Swagger del Trabajo 2, ERS, Arquitectura-MasterMusic.md, Modelo-entidad-relacion-MasterMusic.md y Diagramas-secuencia-MasterMusic.md. Evidencias/swagger.json se conserva como evidencia generada desde el código previo. No se presenta como diseño anterior a ese código.

### API 1

- Conserva rutas de registro/login e instrumentos, 200 de registro/login, 201 de creación y 204 de actualización/eliminación de instrumentos.
- Formaliza DTO de entrada, catálogo público solo activo y lectura administrativa separada. No expone entidades EF ni hashes. Añade nombres de marca/categoría a la proyección pública.
- Define administración de categorías/marcas, activación de instrumentos y entradas de stock.
- Define movimientos internos idempotentes de descuento, devolución y consulta, con JWT y X-Service-Key.
- Establece errores Problem Details y 409 para correo duplicado. **Esto cambia el error de registro previo (400 con mensaje)**; actualizar implementación y pruebas correspondientes cuando se programe.
- Impide eliminación de instrumentos con stock o historial. Requiere futuras migraciones/validaciones; no elimina migraciones antiguas.

### API 2

- Diseña alta, consulta propia y administrativa, confirmación y cancelación de pedidos.
- Define Idempotency-Key por usuario y tipo de comando; idOperacion interno generado por servidor.
- Distingue 201/200 de pedido aceptado, 202 de procesamiento incierto y errores estructurados.
- Añade consulta por clave, consulta por ID y recuperación explícita para respuesta perdida y fallos entre APIs.
- Fija timeout por HTTP interno de cinco segundos, devoluciones únicas y ausencia de transacción distribuida.

### Precisiones de diseño

- Precios CLP enteros; total int64 limitado al entero seguro de JavaScript antes del descuento.
- Catálogo público incluye nombres de marca y categoría sin abrir mantenimiento al público.
- No agrega política de contraseña, pagos, despachos, reseñas, Docker ni una arquitectura adicional.
- Los contratos son diseño; la implementación de API 2, React y las ampliaciones de API 1 sigue siendo trabajo posterior. La existencia del archivo no acredita funcionalidad ni pruebas aprobadas.

En cambios posteriores registrar fecha, rutas/esquemas modificados, motivo, compatibilidad y pruebas realmente ejecutadas. No anotar como realizado un despliegue o prueba sin evidencia.


## 1.0.1 — 2026-11-22 — Ajustes de validación del cierre

La versión documental registra cambios de implementación sobre las mismas rutas `/api/v1.0`.

- `POST /Auth/registro` devuelve `409` con Problem Details y código `CORREO_DUPLICADO` al encontrar un correo repetido o una colisión simultánea al insertar. Se cumple así la respuesta prevista en 1.0.0, reemplazando el `400` heredado.
- Se incorpora la migración `CorreoUsuarioUnico` y el índice único `IX_Usuario_email`. Si existen correos duplicados previos, la migración se detiene para revisarlos; no elimina ni combina cuentas.
- `POST /Auth/login` conserva `401` ante credenciales no aceptadas y cambia su error a Problem Details con código `CREDENCIALES_INVALIDAS`.
- `DELETE /Instrumentos/{id}` exige stock cero y ausencia de historial. Devuelve `409 STOCK_NO_CERO` cuando quedan existencias y `409 RECURSO_EN_USO` cuando hay referencias. La comprobación se realiza dentro de la transacción que bloquea el instrumento.

El cliente React interpreta `detail` y `codigo` además de los campos de error heredados, por lo que estos cambios no requieren modificar sus formularios. La respuesta correcta de registro sigue siendo `200`; el login correcto conserva token y datos públicos del usuario.

Las pruebas agregadas cubren rechazo y conservación de stock, eliminación válida, competencia entre eliminar y sumar stock, correo repetido, altas simultáneas, la restricción SQL y la correspondencia entre modelo y snapshot. La suite define 99 casos .NET; el resultado de ejecución se acredita mediante el TRX del commit probado.

La exportación final de las APIs y su comparación integral con el diseño de 1.0.0 constituyen una comprobación distinta de estos ajustes. Algunos errores heredados de instrumentos todavía utilizan `message`. La implementación recupera altas inciertas, pero no ejecuta la compensación automática propuesta inicialmente.


## 1.0.2 — 2026-11-22 — Documentación de la implementación en Swagger

Se completan metadatos; no se alteran rutas, reglas de negocio, bases de datos ni migraciones.

- La API de catálogo deja de declarar Bearer como requisito global. Registro, login y lectura pública se documentan sin JWT; administración exige Bearer e inventario interno exige Bearer y X-Service-Key en el mismo requisito.
- Se describen los esquemas de respuestas correctas, los errores y los estados 201/204 del catálogo, y 200/201/202 de pedidos, con Location y Retry-After cuando corresponde.
- El parámetro de versión del catálogo muestra 1.0. Se mantienen las URL existentes.
- Se registran las diferencias de entrada de instrumentos, errores heredados, código de transición inválida y compensación automática en [Comparacion-diseno-implementacion.md](Comparacion-diseno-implementacion.md).
- Se agregan cuatro comprobaciones de contratos: la suite pasa de 99 a 103 casos definidos; la selección Swagger contiene 11. La compilación y los resultados ejecutados se acreditan por separado.

Las exportaciones de Implementacion deben regenerarse desde ambas APIs reiniciadas con este código. Los JSON originales de diseño permanecen conservados.
