# Mediciones de cierre de MasterMusic

Ejecución: 35784bea-43f4-4c46-ac98-769df14ba8da. Inicio UTC: 2026-11-22T19:57:43.881Z. Fin UTC: 2026-11-22T20:28:44.484Z. Commit: 029e995.

## Condiciones

APIs HTTPS locales en 7052 y 7267, con las bases de datos reales. Consultas HTTP secuenciales mediante curl de Windows, con validación del certificado. El tiempo medido es time_total de curl, incluyendo la conexión y la descarga completa. No se reutilizan procesos curl entre consultas.

Sistema: Windows_NT 10.0.26300. CPU: 13th Gen Intel(R) Core(TM) i7-13620H. RAM: 16 GB. Node: v24.22.3. SDK .NET: 10.0.401. Cliente: curl 8.22.0 (Windows) libcurl/8.22.0 Schannel zlib/1.3.2 WinIDN WinLDAP.

Catálogo inicial: 2 instrumentos activos. Para alcanzar exactamente 100 se añadieron instrumentos temporales identificados, con stock cero, sin modificar productos previos. La preparación y limpieza quedan fuera de los tiempos medidos.

## RNF 04 rendimiento

100 de 100 consultas fueron correctas en un máximo de 2000 ms, después de cinco consultas de calentamiento. P95: 72.203 ms. Resultado: **Cumple**.

## RNF 05 disponibilidad

Duración: 1800.0 segundos. Catálogo: 60/60 correctas. Pedidos: 60/60 correctas. Programación: una consulta por API cada 30 segundos, 60 por API.

Sin reinicios según identidad e inicio de procesos al comienzo y final: sí. Intervalos con retraso máximo de cinco segundos: sí. Resultado: **Cumple**.

## Limpieza y límites

98 instrumentos temporales eliminados. No se modificaron los instrumentos anteriores ni los pedidos.

Los CSV contienen las observaciones, también las fallidas. Estas mediciones corresponden a una sola máquina y una ventana local de 30 minutos; no acreditan disponibilidad mensual ni carga concurrente. El reporte no incluye credenciales, JWT ni datos de clientes.
