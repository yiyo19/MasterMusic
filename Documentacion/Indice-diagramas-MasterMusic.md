# Índice de diagramas de MasterMusic

Revisión de implementación: 22 de noviembre de 2026. El código de referencia es el commit 1b13fdc, con las pruebas .NET y React aprobadas en GitHub Actions según la verificación del equipo.

Cada modelo incluye explicación, fuente editable y exportaciones. Las fuentes Mermaid pueden abrirse como texto; los archivos draw.io se editan en diagrams.net. Los PNG permiten insertar los modelos en el informe y los SVG conservan la nitidez al ampliar.

| Modelo exigido | Documento y exportaciones |
| --- | --- |
| Casos de uso UML, actores e include/extend | [Acceso/cliente y administración](Casos-de-uso-MasterMusic.md) |
| Clases de dominio UML | [Catálogo/inventario y pedidos](Diagrama-clases-MasterMusic.md) |
| Entidad–relación de ambas bases | [Catálogo, movimientos y pedidos](Modelo-entidad-relacion-MasterMusic.md) |
| Contexto y contenedores | [Arquitectura](Arquitectura-MasterMusic.md) |
| Tres secuencias | [Login, alta de pedido y recuperación tras fallo](Diagramas-secuencia-MasterMusic.md) |
| Despliegue real | [Documento](Diagrama-despliegue-MasterMusic.md) · [Fuente](Diagrama-despliegue-MasterMusic.mmd) · [SVG](Diagrama-despliegue-MasterMusic.svg) · [PNG](Diagrama-despliegue-MasterMusic.png) |
| Estados del pedido | [Documento](Diagrama-estados-pedido-MasterMusic.md) · [Fuente](Diagrama-estados-pedido-MasterMusic.mmd) · [SVG](Diagrama-estados-pedido-MasterMusic.svg) · [PNG](Diagrama-estados-pedido-MasterMusic.png) |

## Correspondencia con la implementación

Esta revisión incorpora la columna Version de Instrumento, el índice único de correo, ProblemaJson y los estados técnicos que realmente generan los servicios. Las secuencias reflejan las respuestas 201/200/202 y la recuperación con los mismos identificadores. No se representa una compensación automática de altas que el código no ejecuta.

El modelo inicial sigue disponible en la rama docs/semana-2 y en el historial. También se conserva el PNG de casos de uso de semana 1, junto con su fuente extraída. Las revisiones se incorporan en feat/api-pedidos para su posterior integración a master.

Los contratos iniciales y los exportados se comparan en [el registro de diferencias](Contratos/Comparacion-diseno-implementacion.md). La [matriz](Matriz-trazabilidad-MasterMusic.md) enlaza requisitos, historias, rutas y pruebas.
