# MasterMusic.Client

Cliente React de MasterMusic. Consume el catálogo y la autenticación de
MasterMusic.Api, y los pedidos de MasterMusic.Ventas.Api.

## Requisitos

- Node.js LTS para Windows, versión 22.12 o superior. Se comprobó la compilación
  con Node 24.22.3. Descarga oficial: https://nodejs.org/en/download
- Las dos APIs y sus bases SQL Server con las migraciones ya aplicadas.
- El certificado HTTPS de desarrollo debe ser de confianza en el equipo.

## Instalar y ejecutar

Desde la raíz del repositorio, en PowerShell:

    cd .\MasterMusic.Client
    npm.cmd ci
    Copy-Item -LiteralPath .env.example -Destination .env.local
    npm.cmd test
    npm.cmd run build
    npm.cmd run dev

Abre http://localhost:5173. Conserva las dos APIs ejecutándose:
catálogo en https://localhost:7052 y pedidos en https://localhost:7267.

Usa npm.cmd en PowerShell para evitar depender de la política de ejecución de
scripts npm.ps1. No es necesario modificar esa política.

Si ya existe .env.local, conserva sus valores y verifica sus dos direcciones.
El archivo .env.local se ignora en Git. El archivo .env.example sí se entrega.
Las variables VITE_CATALOGO_URL y VITE_PEDIDOS_URL son direcciones públicas.
Nunca pongas Jwt__Key, Inventario__ServiceKey, contraseñas ni conexiones SQL
en variables VITE_: Vite incorpora estas variables al JavaScript del navegador.

## Acceso y roles

- Visitante: catálogo y detalle de instrumentos activos.
- Cliente: registro, inicio y cierre de sesión, selección, envío y consulta
  de sus pedidos.
- Administrador: pedidos de todos los clientes, confirmación y cancelación;
  instrumentos, categorías, marcas, activación y entradas de stock.

El registro público crea exclusivamente cuentas Cliente. Para administrar,
usa una cuenta Administrador existente. No se crean administradores ni
contraseñas predeterminadas desde React.

El JWT se conserva en memoria. Recargar exige iniciar sesión nuevamente.
Una respuesta 401 de una ruta protegida o la expiración del JWT cierra la
sesión. Una respuesta 403 muestra el error y conserva la sesión válida.

## Pedidos y respuestas inciertas

Antes de crear, confirmar o cancelar, se conserva en localStorage la clave de
reintento, el tipo de operación y el comando, separados por identificador de
usuario. No se guarda el JWT ni la contraseña.

Una respuesta perdida, un error de red o un 202 mantiene el comando y bloquea
otro envío. El botón Consultar resultado busca la operación, recupera su
resultado o reenvía el mismo comando con la misma clave si el servidor no lo
había registrado. La selección se vacía cuando el pedido está confirmado por
el servidor como creado; su estado de negocio inicial sigue siendo Pendiente.

No borres el almacenamiento del sitio mientras exista una solicitud incierta.
Después de recargar, inicia sesión con la misma cuenta y usa Consultar resultado.

Las entradas administrativas de stock no admiten reintentos automáticos.
Si se pierde su respuesta, el cliente guarda un aviso: actualiza el inventario
y verifica el resultado antes de aceptar una nueva modificación.

## Configuración del servidor

En Development las APIs permiten CORS desde http://localhost:5173.
Para otra dirección, configura Cors__AllowedOrigins__0 en ambas APIs con el
origen exacto, sin una barra al final. Se admiten varios orígenes mediante
Cors__AllowedOrigins__1, etc. Fuera de Development no se permite ningún origen
si no está configurado. La clave interna de inventario permanece entre APIs.

La instalación de las APIs y sus migraciones se describe en el [README principal](../README.md). Reinicia las APIs si modificas sus variables de entorno.

## Comprobación funcional

Usa datos de prueba identificables; estas acciones modifican la base que
tengas configurada, a diferencia de las pruebas automatizadas aisladas.

1. Ingresa como administrador. Crea una categoría y una marca de prueba, y un
   instrumento con precio 1000 y stock 5.
2. Cierra sesión e ingresa como Cliente. Selecciona 2 unidades, revisa el
   resumen y envía el pedido. Debe aparecer Pendiente y total $2.000.
3. Comprueba que el catálogo muestre stock 3. Recargar pedidos no vuelve a
   descontar stock.
4. Ingresa como administrador. Cancela ese pedido con motivo "Prueba de
   integración"; debe quedar Cancelado y el stock volver a 5.
5. Crea otro pedido de 1 unidad y confírmalo como administrador. Debe quedar
   Confirmado y el stock seguir en 4.
6. Edita nombre o precio del instrumento: el stock debe seguir en 4.
7. Intenta eliminar la categoría, la marca o el instrumento de estos pedidos:
   deben conservarse por sus referencias. El instrumento puede desactivarse.
8. Cierra sesión. El catálogo sigue disponible, pero consultar pedidos exige
   ingresar de nuevo.

Las pruebas automatizadas de las APIs ya cubren pérdida de respuesta,
idempotencia y recuperación; las del cliente verifican que se conserve y
reutilice el mismo comando y su clave.

## Pruebas y compilación

    npm.cmd test
    npm.cmd run build

Las pruebas del cliente se ejecutan con node:test y no escriben en SQL.
GitHub Actions incorpora el trabajo cliente-react, independiente del trabajo
de pruebas de las APIs.

Desde la raíz del repositorio, con SQL Server disponible:

    $env:MASTERMUSIC_TEST_SQL = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
    dotnet test .\MasterMusic.Tests\MasterMusic.Tests.csproj --logger "trx;LogFileName=react-catalogo.trx" --results-directory .\TestResults\React

Se esperan 99 pruebas de API, todas aprobadas y ninguna omitida. Las pruebas
SQL crean y eliminan bases temporales con prefijo MasterMusicCI_; no utilizan
MasterMusicDB ni MasterMusicVentasDB. La cuenta de pruebas necesita permisos
para crear esas bases. No sustituyas su configuración por una conexión de
producción.

## Entrega

npm run build genera dist/. Para publicar, configura las URLs reales de las
APIs antes de compilar, sirve dist/ mediante HTTPS y configura los orígenes
CORS correspondientes. No se incluye alojamiento ni una publicación automática.
