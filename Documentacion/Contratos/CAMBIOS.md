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
