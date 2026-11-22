# Retrospectiva de MasterMusic

Registro de cierre del 22 de noviembre de 2026. Resume dificultades, resultados y mejoras identificadas durante el desarrollo de Roberto Acuña y Francisca Medina. Se basa en los cambios del repositorio y en las pruebas realizadas; no atribuye fechas a reuniones que no quedaron registradas.

## Qué funcionó

Dividir requisitos, diseño e implementación en ramas permitió conservar entregas parciales y reunirlas después mediante pull requests. La separación entre catálogo y pedidos mantuvo una responsabilidad clara por base de datos. Las pruebas progresivas detectaron problemas antes de dar por terminados los flujos: la suite pasó de las validaciones iniciales al cierre de 103 casos .NET y 17 del cliente.

Las pruebas con una base SQL real aportaron evidencia sobre concurrencia, unicidad e integridad que una simulación de repositorios no cubre. Las capturas de React permitieron comprobar el efecto visible de cancelar y confirmar un pedido. Guardar contratos iniciales y exportados facilitó explicar las diferencias sin alterar el historial de diseño.

## Qué dificultó el avance

Cambiar de rama hizo parecer que faltaban archivos en la carpeta local. La integración de las ramas de documentación y autenticación resolvió esa base fragmentada. Al reiniciar PowerShell, algunas variables configuradas solo para el proceso dejaron de estar disponibles; el README pasó a documentar configuración persistente local y nombres de variables.

La primera ejecución de React requirió instalar Node. También se intentó compilar una API que ya estaba ejecutándose, bloqueando su archivo ejecutable. Verificar los puertos y reutilizar las instancias abiertas evita ese fallo.

El diseño inicial proponía compensación automática, mientras que el código implementó recuperación explícita. El cierre requirió ajustar clases, datos, arquitectura, secuencias y Swagger para describir el comportamiento real. El tablero se actualizó en bloque y quedó con tareas finalizadas antes de completar informe y defensa; sus estados necesitan criterios de cierre verificables.

## Mejoras propuestas

| Mejora | Aplicación concreta | Evidencia esperada |
| --- | --- | --- |
| Actualizar Kanban al realizar cada tarea | Asignar responsable, objetivo y criterio de terminado; mover la tarjeta cuando exista el resultado. | Tarjeta con enlace al commit, PR o documento y estado correspondiente. |
| Registrar revisiones de iteración en su momento | Anotar resultado, dificultades y ajuste al finalizar cada tramo. | Registro con fecha real y enlaces, sin reconstruir reuniones inexistentes. |
| Revisar contratos junto con cambios de código | Comprobar rutas, estados, errores y seguridad antes de publicar. | Pruebas de contrato y registro de diferencias actualizado. |
| Mantener una ejecución reproducible | Seguir README y evitar claves en archivos o comandos compartidos. | Dos APIs, React y migraciones ejecutables desde la documentación. |
| Ensayar la defensa de forma cruzada | Cada integrante explica una parte desarrollada por el otro. | Recorrido completo y respuestas sobre JWT, EF Core, roles e idempotencia. |

## Riesgos y límites que se conservan

El despliegue es local y no demuestra disponibilidad de un servicio público. La creación y la cancelación dependen de inventario; una operación incierta debe recuperarse. No hay compensación automática de altas ni revocación inmediata de JWT. Algunas respuestas heredadas conservan el formato message. El cierre identifica estas decisiones para orientar mantenimiento futuro, sin presentarlas como funcionalidades implementadas.

La fecha de validación docente no quedó registrada. La exportación de Trello no contiene asignaciones individuales ni comentarios de revisión. Estos límites de la evidencia se distinguen de las funciones y pruebas efectivamente entregadas.
