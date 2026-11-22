# Colección de pruebas de las APIs de MasterMusic

`MasterMusic.http` contiene 39 solicitudes numeradas para registro y acceso, CRUD de catálogo, roles, creación de pedidos, idempotencia, confirmación, cancelación y consulta de operaciones. El recorrido utiliza las dos APIs reales y modifica datos de prueba identificables.

## Preparación

Inicia catálogo en `https://localhost:7052` y pedidos en `https://localhost:7267`, con sus migraciones y configuración. No es necesario ejecutar React para esta colección.

Crea una copia privada:

~~~powershell
Copy-Item -LiteralPath .\Documentacion\Pruebas\MasterMusic.http -Destination .\Documentacion\Pruebas\MasterMusic.local.http
~~~

La carpeta ignora `*.local.http`. Completa las variables de credenciales y tokens únicamente en esa copia. No incluyas contraseñas ni JWT en capturas o reportes de entrega. Usa un correo nuevo de Cliente para P01 y una cuenta Administrador existente para P03.

Abre la copia en Visual Studio y envía cada solicitud desde **Send Request / Enviar solicitud**, disponible sobre la línea HTTP. Las variables `@Nombre` se utilizan como `{{Nombre}}`. Consulta la [documentación de archivos HTTP de Microsoft](https://learn.microsoft.com/es-es/aspnet/core/test/http-files?view=aspnetcore-10.0).

## Recorrido

1. **P01–P06:** registra el cliente, inicia ambas sesiones y comprueba catálogo público, 401 sin token y 403 del Cliente en administración. Copia los tokens a las variables privadas.
2. **P07–P19:** crea categoría, marca e instrumento con stock cero; actualiza y consulta; elimina primero el instrumento y luego sus referencias. Copia los identificadores devueltos.
3. Repite **P07 y P10** y actualiza sus variables: P20 utilizará nuevas referencias.
4. **P20–P28:** crea un instrumento con stock 5, rechaza borrarlo con existencias, crea un pedido de 2 unidades y cancélalo. El stock pasa de 5 a 3 y vuelve a 5.
5. **P29–P34:** crea otro pedido de una unidad y confírmalo. El stock queda en 4 y no vuelve a descontarse. Una cancelación posterior con una clave nueva debe rechazarse.
6. **P35–P37:** consulta y recupera la operación del primer pedido con la misma cuenta y clave. En este recorrido ya completado, recuperar no duplica efectos.
7. **P38–P39:** consulta los documentos OpenAPI de ambas APIs.

Genera cinco claves UUID nuevas antes del recorrido:

~~~powershell
1..5 | ForEach-Object { [guid]::NewGuid().ToString() }
~~~

Asígnalas a las cinco variables `ClaveCrear1`, `ClaveCancelar1`, `ClaveCrear2`, `ClaveConfirmar2` y `ClaveCancelarFinalizado`. Conserva la misma clave y el mismo cuerpo al comprobar un reintento. Una acción nueva necesita otra clave.

Cada bloque indica el código y el resultado esperado. Si la API devuelve `202` o falla la conexión, conserva la clave y consulta la operación; no continúes como si el pedido estuviera aceptado. Las pruebas automáticas de SQL cubren además respuesta perdida y concurrencia; este recorrido normal no reproduce por sí solo todos esos fallos.

Los pedidos finales y su historial se conservan. El instrumento de pedidos puede desactivarse tras la comprobación; no se elimina para borrar el historial.

## Exportación de contratos

Desde la raíz del proyecto y con ambas APIs ejecutándose, pega estos comandos directamente en PowerShell:

~~~powershell
$catalogoOpenApi = Invoke-WebRequest -UseBasicParsing -Uri "https://localhost:7052/swagger/v1/swagger.json" -TimeoutSec 30
$pedidosOpenApi = Invoke-WebRequest -UseBasicParsing -Uri "https://localhost:7267/openapi/v1.json" -TimeoutSec 30
$documentoCatalogo = $catalogoOpenApi.Content | ConvertFrom-Json
$documentoPedidos = $pedidosOpenApi.Content | ConvertFrom-Json
if ($documentoCatalogo.openapi -notmatch '^3\.' -or $documentoPedidos.openapi -notmatch '^3\.' -or -not $documentoCatalogo.paths -or -not $documentoPedidos.paths) { throw "Revisa las respuestas: deben ser documentos OpenAPI con rutas." }
$destinoOpenApi = Join-Path (Get-Location).Path "Documentacion\Contratos\Implementacion"
New-Item -ItemType Directory -Path $destinoOpenApi -Force | Out-Null
$utf8OpenApi = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $destinoOpenApi "mastermusic-api.openapi.json"), $catalogoOpenApi.Content, $utf8OpenApi)
[IO.File]::WriteAllText((Join-Path $destinoOpenApi "mastermusic-ventas-api.openapi.json"), $pedidosOpenApi.Content, $utf8OpenApi)
Get-ChildItem -LiteralPath $destinoOpenApi -Filter "*.json" | Select-Object Name,Length
~~~

Se consultan ambas APIs sin credenciales y se guardan sus documentos en `Documentacion/Contratos/Implementacion`. Los contratos de diseño permanecen intactos en la carpeta superior. Estos comandos no ejecutan archivos `.ps1` ni cambian la política de ejecución. El contraste posterior de rutas, esquemas, respuestas y permisos completa la revisión.
## Evidencia automatizada

El cierre técnico de 22-11-2026 fue ejecutado por el equipo con **99 pruebas .NET aprobadas, 0 fallidas y 0 omitidas**. El archivo `TestResults/Cierre/cierre-tecnico.trx` contiene esos resultados. La colección manual describe solicitudes reproducibles; no se presenta como ejecutada por el solo hecho de guardar este archivo.

Guarda el TRX en `Documentacion/Evidencias/Semana-3/07-pruebas-cierre-tecnico.trx` para que forme parte de la entrega. Si se modifica código posteriormente, conserva el reporte de la nueva ejecución y asócialo al commit correspondiente.
