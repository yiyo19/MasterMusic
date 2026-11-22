# Gestión e iteraciones de MasterMusic

Registro de cierre elaborado el 22 de noviembre de 2026 por Roberto Acuña y Francisca Medina. Consolida el plan Kanban, el historial de Git, los entregables y la exportación del tablero. Las revisiones que siguen se documentan en esta fecha; las fechas de los commits muestran la evolución del producto y no se presentan como actas de reuniones anteriores.

## Metodología y alcance

Se utilizó Kanban para organizar tareas en Product backlog, Haciendo, Impedimento, Revisión y Finalizado. El plan estableció tres iteraciones: requerimientos y diseño; construcción e integración; y verificación y entrega. Las semanas de la pauta y estas iteraciones no tienen que coincidir una a una: la primera agrupó los hitos de requerimientos y diseño.

La ERS contiene 22 requisitos funcionales y 18 historias, clasificados Must para esta versión. Pagos, despachos, facturación, reseñas y múltiples sucursales quedaron fuera del alcance. Los colores sin nombre de las tarjetas no se interpretan como una priorización que el tablero no especifica.

## Iteración 1 Requerimientos y diseño

**Objetivo:** precisar el caso MasterMusic, sus roles y reglas; diseñar los modelos y los contratos de ambas APIs antes de construir el nuevo flujo de pedidos.

**Periodo de cambios verificables:** del 3 al 10 de noviembre de 2026. ERS e historias se incorporaron en b3709d7 y 5454afc. El plan se registró en de97b4c; los casos de uso e informe de semana 1 tienen cambios de Francisca entre 4d717f4 y b7f72d2. Arquitectura, clases, ER y secuencias se registraron entre 2f1c162 y 8350c0c. Los contratos OpenAPI llegaron en 5efe9c0, antes del inicio de la segunda API en c5aaa18.

**Revisión de resultados:** el alcance distingue Visitante, Cliente y Administrador; define pedidos Pendientes, Confirmados y Cancelados, y separa identidad/catálogo de pedidos. El análisis documental de Audiomúsica fundamenta la información comercial, disponibilidad y resumen de compra. No se realizaron entrevistas a clientes reales ni se atribuyen procesos internos de Audiomúsica al equipo.

**Ajustes posteriores:** los contratos y modelos iniciales incluían compensación automática y algunas estructuras que no se implementaron de esa forma. La comparación de contratos y los diagramas actualizados conservan el diseño original y explican las diferencias.

## Iteración 2 Construcción e integración

**Objetivo:** ejecutar dos APIs con bases reales y conectar los flujos de cliente y administrador mediante React.

**Periodo de cambios verificables:** del 16 al 22 de noviembre de 2026. c5aaa18 agrega la API de pedidos y su migración; 8264aa7 incorpora la consulta propia con JWT; 33be1de agrega creación, confirmación, cancelación y control de stock; d25e7d0 incorpora React y evidencias; 12f29ba habilita Swagger de pedidos.

**Revisión de resultados:** se registran y consultan pedidos de su propietario, se descuenta stock una vez, la confirmación conserva ese descuento y la cancelación devuelve las cantidades una vez. El cliente HTTP tipado conecta pedidos con inventario. Las pruebas y las capturas funcionales respaldan esos comportamientos.

**Dificultades y ajustes:** al trabajar en ramas diferentes, la carpeta local mostraba contenidos distintos. Se integraron semana 1, semana 2 y autenticación a master mediante los PR 2, 3 y 4, conservando el historial, y se continuó en feat/api-pedidos. También se corrigió la ubicación del contexto de pedidos, se instaló Node para React y se consolidaron las variables locales necesarias para reiniciar las APIs.

## Iteración 3 Verificación y entrega

**Objetivo:** verificar coherencia de stock, permisos y recuperación; completar trazabilidad, contratos reales, diagramas y documentación de ejecución.

**Cambios verificables al 22 de noviembre de 2026:** 3dcd4c3 registra despliegue, 86bfde4 estados, 00404e3 README y configuración, 9ad1e08 validaciones finales/ADR/trazabilidad, 1b13fdc metadatos Swagger y comparación de contratos, 029e995 incorpora diagramas alineados con el código y sus fuentes/exportaciones, y 163cf08 conserva las mediciones finales de rendimiento y disponibilidad.

**Revisión de resultados:** el TRX 08-pruebas-contratos-cierre registra 103 pruebas .NET aprobadas, sin fallos ni omisiones, incluidas 50 con SQL Server. El cliente cuenta con 17 pruebas aprobadas y compilación de producción; el equipo confirmó ambos trabajos de Actions en verde para 1b13fdc. Las capturas muestran cancelación, devolución, confirmación y conservación del stock. La [medición local de RNF-04/05](Evidencias/Cierre/RNF-2026-11-22T19-57-43-840Z-35784bea/README.md) concluyó con 100/100 consultas dentro de dos segundos (P95 72,203 ms) y 60/60 lecturas por API durante 30 minutos, sin reinicios detectados. Se eliminaron los 98 instrumentos temporales. Estos resultados se registran por separado de la suite funcional.

**Cierre documental:** se incorporan las mediciones reales, el registro de iteraciones, la retrospectiva y la exportación del tablero. El informe técnico PDF se publicará por separado después de los ajustes de edición. **Integración:** el equipo confirmó el estado Merged de la integración de feat/api-pedidos a master. **Actividades de entrega:** comprobar las verificaciones de master y ensayar la demostración y la defensa. Una tarjeta Finalizado que aún no satisface su criterio debe volver a una lista de trabajo.

## Aportes registrados en Git

| Integrante | Aportes identificables | Ejemplos |
| --- | --- | --- |
| Roberto Acuña | Requisitos y contratos iniciales; API de pedidos, integración, cliente React y evidencias funcionales; despliegue. | 5454afc, 5efe9c0, c5aaa18, 33be1de, d25e7d0, 3dcd4c3. |
| Francisca Medina | Casos de uso e informe de semana 1; organización de evidencias; estados, README, validaciones de cierre, ADR, trazabilidad, contratos reales y modelado actualizado. | 4d717f4, 472625f, b7f72d2, 86bfde4, 00404e3, 9ad1e08, 1b13fdc, 029e995. |

Al corte 029e995, el tramo del Trabajo 3 desde 27580d3 contiene 17 commits sin fusiones con autoría de Roberto y 16 con autoría de Francisca. El conteo describe el historial, no una equivalencia de esfuerzo. Ambos integrantes deben poder explicar el sistema completo en la defensa.

Los PR 2, 3 y 4 quedaron integrados el 16 de noviembre, con commits d1648b5, de661d0 y 18d9788. Una fusión no demuestra por sí sola una revisión entre pares: se debe conservar la revisión real en GitHub cuando se realice. No se atribuyen aprobaciones a otra persona sin su participación.

## Validación del caso y evidencias

El equipo confirma que el docente Enso Guidotti Robles aprobó el caso MasterMusic. La fecha exacta y una constancia fechada no se conservaron en los materiales disponibles de este cierre. La tarjeta 2 figura Finalizado, sin descripción o comentario que establezca una fecha de aprobación; por ello no se usa la fecha de movimiento de la tarjeta como fecha de aprobación del docente.

Fuentes: [plan](Plan-de-trabajo-MasterMusic.docx), [avance de semana 1](Informe-Avance-Semana-1.md), [tablero exportado](Evidencias/Gestion/README.md), [matriz](Matriz-trazabilidad-MasterMusic.md), [comparación de contratos](Contratos/Comparacion-diseno-implementacion.md) y [retrospectiva](Retrospectiva-MasterMusic.md).
