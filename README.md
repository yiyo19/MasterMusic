# MasterMusic — Trabajo 3

Sistema de catálogo, inventario y pedidos de una tienda de instrumentos musicales. El cliente React se conecta a dos APIs ASP.NET Core con controladores y bases SQL Server independientes. La API de pedidos consulta y modifica el inventario mediante HTTPS.

**Asignatura:** Construcción de Software · **Integrantes:** Roberto Acuña y Francisca Medina.

## Accesos locales

Con los tres servicios iniciados:

| Componente | Dirección |
| --- | --- |
| Aplicación React | [http://localhost:5173/](http://localhost:5173/) |
| Swagger del catálogo y autenticación | [https://localhost:7052/swagger](https://localhost:7052/swagger) |
| Swagger de pedidos | [https://localhost:7267/swagger](https://localhost:7267/swagger) |
| OpenAPI generado del catálogo | [https://localhost:7052/swagger/v1/swagger.json](https://localhost:7052/swagger/v1/swagger.json) |
| OpenAPI generado de pedidos | [https://localhost:7267/openapi/v1.json](https://localhost:7267/openapi/v1.json) |

Swagger y los documentos generados se habilitan en `Development`. La aplicación React se abre en la raíz del puerto 5173.

## Funcionalidades

| Acceso | Funciones disponibles |
| --- | --- |
| Visitante | Consultar instrumentos activos, buscar y filtrar el catálogo, ver el detalle, registrarse e iniciar sesión. |
| Cliente | Crear pedidos y consultar exclusivamente sus propios pedidos. |
| Administrador | Gestionar instrumentos, categorías y marcas; activar o desactivar instrumentos, sumar stock, consultar todos los pedidos y confirmar o cancelar los pendientes. |

El registro público asigna exclusivamente el rol `Cliente`. El administrador inicial se crea desde la configuración del servidor. Las contraseñas se almacenan como **hash BCrypt** y las rutas protegidas validan JWT y roles.

### Reglas de pedidos e inventario

- El servidor comprueba instrumentos activos, cantidades enteras positivas, precios y stock antes de aceptar un pedido.
- Al crear el pedido se descuenta el stock una sola vez y el pedido queda `Pendiente`. Los precios se expresan en pesos chilenos, sin decimales.
- Confirmar cambia el estado a `Confirmado` y conserva el stock descontado al crear.
- Cancelar exige un motivo y devuelve el stock una sola vez antes de dejar el pedido `Cancelado`.
- `Confirmado` y `Cancelado` son estados finales. Los pedidos permanecen disponibles para consulta.
- Los detalles conservan el nombre y precio aplicados, aunque después cambie el catálogo.
- Crear, confirmar y cancelar usan una clave de idempotencia: repetir la misma solicitud con la misma clave recupera el resultado sin duplicar sus efectos.
- Una respuesta perdida conserva una operación recuperable. El cliente permite consultar su resultado y bloquea un nuevo envío mientras exista incertidumbre.
- Editar datos de un instrumento no modifica su stock. Las entradas de existencias y la activación tienen acciones propias.
- Un instrumento solo se elimina si su stock es cero y no tiene historial. Las referencias también protegen la eliminación de categorías y marcas; un instrumento puede desactivarse.

El alcance implementado es catálogo, inventario y pedidos. No incluye cobro con pasarela de pago, despacho ni facturación. Las entidades heredadas `Venta`, `DetalleVenta` y `Resena` permanecen en el modelo original; el flujo de pedidos del Trabajo 3 utiliza la segunda API.

## Arquitectura y estructura

~~~mermaid
flowchart LR
    U["Usuario"] --> R["React · localhost:5173"]
    R -->|"HTTPS · catálogo y acceso"| A["MasterMusic.Api · 7052"]
    R -->|"HTTPS · JWT y pedidos"| B["MasterMusic.Ventas.Api · 7267"]
    B -->|"IHttpClientFactory · HTTPS · JWT + X-Service-Key"| A
    A --> D1[("MasterMusicDB")]
    B --> D2[("MasterMusicVentasDB")]
~~~

Cada API accede a su propia base mediante EF Core. La integración entre APIs no utiliza consultas ni claves foráneas entre bases. El cliente HTTP tipado de inventario tiene un tiempo de espera de cinco segundos; las operaciones y movimientos persistidos permiten recuperar resultados ante interrupciones.

~~~text
MasterMusic/
├── MasterMusic.Api/          # Catálogo, autenticación e inventario
├── MasterMusic.Ventas.Api/   # Pedidos y recuperación de operaciones
├── MasterMusic.Client/       # React, Vite y pruebas del cliente
├── MasterMusic.Tests/        # Pruebas de las APIs
├── Documentacion/            # Requisitos, diseños, contratos y evidencias
├── .github/workflows/        # Integración continua
├── MasterMusic.slnx          # Solución de los tres proyectos .NET
├── dotnet-tools.json         # Herramienta local dotnet-ef
└── .env.example              # Referencia de variables, sin secretos
~~~

Tecnologías utilizadas: .NET 10, ASP.NET Core con controladores, EF Core 10.0.13, SQL Server, JWT Bearer, BCrypt, React 19.3.1 y Vite 8.3.8. Las dependencias exactas están en los archivos `.csproj` y `package-lock.json`.

## Instalación inicial en Windows

Si la aplicación ya funciona en este computador, pasa a [Iniciar después de reiniciar el computador](#iniciar-después-de-reiniciar-el-computador).

### 1. Requisitos y código

Se necesita Git, el SDK .NET 10, Node.js con npm y una instancia SQL Server accesible. El cliente se comprobó con Node.js **24.22.3**; `package.json` requiere al menos 22.12. SQL Server Express con autenticación de Windows se utilizó para la ejecución local.

Comprueba las herramientas en una nueva ventana de PowerShell:

~~~powershell
git --version
dotnet --version
node --version
npm.cmd --version
~~~

Si todavía no tienes una copia:

~~~powershell
git clone https://github.com/yiyo19/MasterMusic.git
cd .\MasterMusic
git switch feat/api-pedidos
~~~

El avance del Hito 3 se encuentra en `feat/api-pedidos`. Después de integrar esa rama en `master`, la rama principal también contendrá estos archivos. Las ramas de semanas anteriores conservan su historial.

Si ya tienes el proyecto, trabaja en su carpeta existente. Revisa `git status` antes de cambiar de rama; conserva cualquier modificación local.

### 2. Configurar el entorno local

La plantilla [`.env.example`](.env.example) enumera las variables. Es una referencia: **ASP.NET Core no carga automáticamente ese archivo**. La configuración se establece mediante variables de entorno.

| Variable | Uso |
| --- | --- |
| `ConnectionStrings__MasterMusic` | Conexión SQL de la API de catálogo. |
| `ConnectionStrings__MasterMusicVentas` | Conexión SQL de la API de pedidos, a otra base. |
| `Jwt__Key` | Clave privada compartida por ambas APIs para firmar y validar JWT. |
| `Jwt__Issuer` / `Jwt__Audience` | Deben coincidir en ambas APIs; valores del proyecto: `MasterMusicApi` y `MasterMusicClients`. |
| `Jwt__ExpireMinutes` | Duración del token emitido por el catálogo; 60 minutos si no se configura. |
| `Admin__Email` / `Admin__Password` | Creación inicial de una cuenta Administrador en el catálogo. |
| `Inventario__BaseUrl` | Dirección HTTPS del catálogo que utiliza pedidos; localmente `https://localhost:7052/`. |
| `Inventario__ServiceKey` | Clave privada compartida para la comunicación interna de inventario. |
| `Cors__AllowedOrigins__0` | Origen del cliente permitido en ambas APIs; localmente `http://localhost:5173`. |

Este bloque configura una instancia local `.\SQLEXPRESS` con autenticación de Windows. **Adapta el servidor si tu instancia tiene otro nombre.** Conserva los valores que ya existan, genera claves aleatorias únicamente cuando faltan y solicita las credenciales iniciales sin mostrarlas. Guarda la configuración en las variables de usuario de Windows y en la ventana actual.

~~~powershell
& {
    function Leer-ConfiguracionLocal([string]$nombre) {
        $valor = [Environment]::GetEnvironmentVariable($nombre, 'Process')
        if ([string]::IsNullOrWhiteSpace($valor)) {
            $valor = [Environment]::GetEnvironmentVariable($nombre, 'User')
        }
        return $valor
    }

    function Guardar-ConfiguracionLocal([string]$nombre, [string]$valor) {
        if ([string]::IsNullOrWhiteSpace($valor)) {
            throw "La variable $nombre no puede quedar vacia."
        }
        [Environment]::SetEnvironmentVariable($nombre, $valor, 'User')
        [Environment]::SetEnvironmentVariable($nombre, $valor, 'Process')
    }

    $valoresLocales = @{
        ConnectionStrings__MasterMusic = 'Server=.\SQLEXPRESS;Database=MasterMusicDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
        ConnectionStrings__MasterMusicVentas = 'Server=.\SQLEXPRESS;Database=MasterMusicVentasDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
        Jwt__Issuer = 'MasterMusicApi'
        Jwt__Audience = 'MasterMusicClients'
        Jwt__ExpireMinutes = '60'
        Inventario__BaseUrl = 'https://localhost:7052/'
        Cors__AllowedOrigins__0 = 'http://localhost:5173'
    }

    foreach ($nombre in $valoresLocales.Keys) {
        $valor = Leer-ConfiguracionLocal $nombre
        if ([string]::IsNullOrWhiteSpace($valor)) { $valor = $valoresLocales[$nombre] }
        Guardar-ConfiguracionLocal $nombre $valor
    }

    foreach ($nombre in @('Jwt__Key', 'Inventario__ServiceKey')) {
        $valor = Leer-ConfiguracionLocal $nombre
        if ([string]::IsNullOrWhiteSpace($valor)) {
            $bytes = New-Object byte[] 32
            $generador = [System.Security.Cryptography.RandomNumberGenerator]::Create()
            try { $generador.GetBytes($bytes) } finally { $generador.Dispose() }
            $valor = [Convert]::ToBase64String($bytes)
        }
        Guardar-ConfiguracionLocal $nombre $valor
    }

    $correo = Leer-ConfiguracionLocal 'Admin__Email'
    if ([string]::IsNullOrWhiteSpace($correo)) {
        $correo = Read-Host 'Correo para crear la cuenta Administrador'
    }
    Guardar-ConfiguracionLocal 'Admin__Email' $correo

    $clave = Leer-ConfiguracionLocal 'Admin__Password'
    if ([string]::IsNullOrWhiteSpace($clave)) {
        $segura = Read-Host 'Elige una contrasena privada para el Administrador' -AsSecureString
        $credencial = [System.Management.Automation.PSCredential]::new($correo, $segura)
        $clave = $credencial.GetNetworkCredential().Password
    }
    Guardar-ConfiguracionLocal 'Admin__Password' $clave
    'Configuracion local guardada. Los secretos no se han mostrado.'
}
~~~

La configuración usa certificados SQL de desarrollo de confianza local. Para un servidor publicado deben utilizarse certificados válidos y la configuración correspondiente.

El administrador se crea al iniciar la API de catálogo si ese correo todavía no existe. **Cambiar `Admin__Password` no cambia la contraseña de una cuenta ya creada**, ni convierte en administrador a un cliente existente. Para una nueva instalación, elige un correo de administrador distinto del que usarás para registrarte como cliente.

Las nuevas ventanas heredan las variables guardadas. Si Visual Studio o la terminal estaban abiertos antes del cambio, reinícialos para que las carguen. No copies claves, contraseñas ni cadenas con credenciales al repositorio.

### 3. Restaurar y aplicar migraciones

Desde la raíz del repositorio, con SQL Server iniciado:

~~~powershell
dotnet tool restore
dotnet restore .\MasterMusic.slnx
dotnet dev-certs https --trust
dotnet build .\MasterMusic.slnx

dotnet ef database update --project .\MasterMusic.Api --startup-project .\MasterMusic.Api --context MasterMusicContext
dotnet ef database update --project .\MasterMusic.Ventas.Api --startup-project .\MasterMusic.Ventas.Api --context MasterMusicVentasContext
~~~

Acepta el aviso de confianza del certificado HTTPS de desarrollo. Las migraciones incluidas son `InitialCreate` e `InventarioPedidos` en catálogo, además de CorreoUsuarioUnico para impedir correos repetidos, e `InicialPedidos` y `OperacionesPedidos` en pedidos. El comando aplica las migraciones pendientes; no se deben borrar bases ni generar nuevamente estas migraciones para instalar el proyecto.

### 4. Instalar el cliente

~~~powershell
cd .\MasterMusic.Client
npm.cmd ci
if (-not (Test-Path -LiteralPath .\.env.local)) {
    Copy-Item -LiteralPath .\.env.example -Destination .\.env.local
}
cd ..
~~~

La plantilla propia del cliente [`MasterMusic.Client/.env.example`](MasterMusic.Client/.env.example) contiene únicamente `VITE_CATALOGO_URL` y `VITE_PEDIDOS_URL`. Sus valores locales ya coinciden con los puertos de las APIs. Las variables `VITE_` son públicas en el navegador: no deben contener secretos.

## Iniciar después de reiniciar el computador

No es necesario volver a instalar paquetes ni crear migraciones en cada sesión. SQL Server debe estar iniciado y la configuración anterior debe estar disponible.

Abre **tres ventanas de PowerShell**. En cada una entra primero a la raíz de tu copia del repositorio, mediante `cd "ruta completa de MasterMusic"`. Ejecuta un bloque diferente por ventana.

**Ventana 1 — catálogo y autenticación:**

~~~powershell
dotnet run --project .\MasterMusic.Api\MasterMusic.Api.csproj --launch-profile https
~~~

**Ventana 2 — pedidos:**

~~~powershell
dotnet run --project .\MasterMusic.Ventas.Api\MasterMusic.Ventas.Api.csproj --launch-profile https
~~~

**Ventana 3 — React:**

~~~powershell
cd .\MasterMusic.Client
npm.cmd run dev
~~~

Espera `Now listening on` en cada API y `Local: http://localhost:5173/` en React. Que la consola permanezca ocupada es normal: el servidor sigue atendiendo solicitudes. Conserva las tres ventanas abiertas y abre [MasterMusic](http://localhost:5173/). Para detener un servicio, usa **Ctrl+C** en su propia ventana.

## Comprobación funcional desde React

Estas acciones usan las bases locales configuradas; elige instrumentos y cuentas de prueba.

1. Ingresa como Administrador y crea una categoría, una marca y un instrumento con precio `1000` y stock `5`.
2. Sal y registra una cuenta Cliente. Selecciona dos unidades, revisa el resumen y envía el pedido.
3. Comprueba un pedido `Pendiente` por `$2.000 CLP` y stock `3`.
4. Ingresa como Administrador y cancela ese pedido con un motivo. Debe quedar `Cancelado` y el stock volver a `5`.
5. Crea otro pedido de una unidad con el Cliente y confírmalo como Administrador. Debe quedar `Confirmado` y el stock mantenerse en `4`.
6. Consulta los pedidos con el Cliente: solo deben aparecer los de esa cuenta.

El JWT se mantiene en memoria. Después de recargar la página puede ser necesario iniciar sesión otra vez. Una respuesta `401` en una ruta protegida cierra la sesión; un `403` informa que el rol no tiene acceso.

Si una escritura queda con resultado incierto, vuelve a entrar con la misma cuenta y utiliza **Consultar resultado**. No borres el almacenamiento del sitio ni envíes otro pedido con una clave nueva mientras se recupera el anterior. Los comandos pendientes se conservan por cuenta en `localStorage`; el token y la contraseña no se guardan allí.

## Endpoints principales

Todos los paths de esta tabla incluyen la versión `v1.0`. Los detalles de cuerpos y respuestas están en Swagger.

### API de catálogo — puerto 7052

| Método | Ruta | Acceso y propósito |
| --- | --- | --- |
| POST | `/api/v1.0/Auth/registro` | Público; registro de Cliente. |
| POST | `/api/v1.0/Auth/login` | Público; emite JWT al validar credenciales. |
| GET | `/api/v1.0/Instrumentos` y `/{id}` | Público; instrumentos activos. |
| GET | `/api/v1.0/admin/Instrumentos` y `/{id}` | Administrador; incluye instrumentos inactivos. |
| POST | `/api/v1.0/Instrumentos` | Administrador; crea un instrumento. |
| PUT / DELETE | `/api/v1.0/Instrumentos/{id}` | Administrador; edición o eliminación con stock cero y sin historial. |
| PATCH | `/api/v1.0/Instrumentos/{id}/estado` | Administrador; activa o desactiva. |
| POST | `/api/v1.0/Instrumentos/{id}/existencias` | Administrador; suma una cantidad positiva al stock. |
| GET / POST | `/api/v1.0/Categorias` y `/api/v1.0/Marcas` | Administrador; lista o crea catálogos auxiliares. |
| GET / PUT / DELETE | `/api/v1.0/Categorias/{id}` y `/api/v1.0/Marcas/{id}` | Administrador; consulta, edición o eliminación. |
| POST | `/api/v1.0/interno/Movimientos/descuentos` y `/devoluciones` | Integración entre APIs; requiere JWT autorizado y `X-Service-Key`. |
| GET | `/api/v1.0/interno/Movimientos/{idOperacion}` | Integración entre APIs; consulta un movimiento persistido. |

El catálogo público de instrumentos incluye los datos de categoría y marca que necesita React. Los endpoints de gestión de categorías y marcas requieren Administrador.

### API de pedidos — puerto 7267

| Método | Ruta | Acceso y propósito |
| --- | --- | --- |
| GET | `/api/v1.0/Pedidos` y `/{idPedido}` | Cliente; únicamente sus pedidos. |
| POST | `/api/v1.0/Pedidos` | Cliente; crea un pedido y coordina el descuento. |
| GET | `/api/v1.0/admin/Pedidos` y `/{idPedido}` | Administrador; todos los pedidos. |
| POST | `/api/v1.0/admin/Pedidos/{idPedido}/confirmacion` | Administrador; confirma un pendiente. |
| POST | `/api/v1.0/admin/Pedidos/{idPedido}/cancelacion` | Administrador; cancela y coordina la devolución. |
| GET | `/api/v1.0/Operaciones?claveCliente={uuid}&tipo={tipo}` | Actor propietario; búsqueda por clave y tipo `Crear`, `Confirmar` o `Cancelar`. |
| GET | `/api/v1.0/Operaciones/{idOperacion}` | Actor propietario; consulta el resultado. |
| POST | `/api/v1.0/Operaciones/{idOperacion}/recuperacion` | Actor propietario con el rol de la operación; recupera un resultado incierto. |

Para crear, confirmar o cancelar, envía `Authorization: Bearer <token>` y `Idempotency-Key: <uuid>`. Cada nueva operación usa una clave distinta; un reintento conserva la clave y el cuerpo originales.

El cuerpo de creación tiene `lineas` con `idInstrumento`, `cantidad` y `precioEsperado`. La confirmación no requiere cuerpo; la cancelación envía `motivo`. Los identificadores y precios deben corresponder al catálogo consultado.

Las validaciones utilizan `400`; la autenticación y permisos, `401` y `403`; un recurso no visible o inexistente, `404`; y los conflictos de negocio o concurrencia, `409`. Un `202` con operación recuperable no confirma todavía el resultado de negocio.

Para probar con Swagger, inicia sesión en el catálogo, copia el `token` devuelto y pégalo en **Authorize** del Swagger que usarás. Introduce solo el token. No publiques capturas con el JWT ni con credenciales.

## Pruebas e integración continua

La versión actual define **103 casos de prueba .NET**, incluidos **50 con SQL Server**, y **17 pruebas del cliente React**.

Para probar localmente, detén las APIs con Ctrl+C si sus procesos mantienen bloqueados los archivos de compilación. Desde la raíz:

~~~powershell
$env:MASTERMUSIC_TEST_SQL = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test .\MasterMusic.Tests\MasterMusic.Tests.csproj --configuration Release --logger "trx;LogFileName=pruebas.trx" --results-directory .\TestResults
~~~

Adapta el nombre de la instancia si corresponde. La cuenta SQL debe poder crear y eliminar bases de prueba. La fixture crea bases únicas con prefijos `MasterMusicCI_Catalogo_` y `MasterMusicCI_Ventas_`, y las elimina al terminar; no utiliza las bases funcionales `MasterMusicDB` ni `MasterMusicVentasDB`.

Si falta `MASTERMUSIC_TEST_SQL`, se omiten los 50 casos SQL. Una ejecución completa debe mostrar **103 aprobadas, 0 fallidas y 0 omitidas**. El reporte se guarda en `TestResults/pruebas.trx`.

Para el cliente:

~~~powershell
cd .\MasterMusic.Client
npm.cmd test
npm.cmd run build
cd ..
~~~

Se esperan 17 pruebas aprobadas y una compilación correcta. `npm.cmd run build` genera `MasterMusic.Client/dist`, que no se versiona. Las pruebas del cliente usan `node:test` y no modifican SQL Server.

El workflow [`pruebas.yml`](.github/workflows/pruebas.yml) ejecuta dos trabajos:

- **pruebas:** restaura, compila y prueba .NET con un contenedor SQL Server de pruebas; conserva el reporte TRX como artefacto `resultados-pruebas`.
- **cliente-react:** instala con `npm ci`, ejecuta las 17 pruebas y compila React.

En GitHub, abre **Actions → Pruebas MasterMusic** y selecciona la ejecución del commit correspondiente. Ambos trabajos deben estar en verde. Un resultado de un commit anterior no sustituye la comprobación del nuevo avance.

## Documentación y evidencias

| Entregable | Archivo |
| --- | --- |
| Requisitos | [ERS](Documentacion/ERS-MasterMusic.docx) |
| Plan y avance inicial | [Plan de trabajo](Documentacion/Plan-de-trabajo-MasterMusic.docx) · [Informe de semana 1](Documentacion/Informe-Avance-Semana-1.md) |
| Análisis documental | [Análisis Audiomúsica](Documentacion/AD-01-analisis-audiomusica.docx) |
| Modelado completo | [Índice de diagramas, fuentes y exportaciones](Documentacion/Indice-diagramas-MasterMusic.md) |
| Casos de uso | [Vistas UML actualizadas](Documentacion/Casos-de-uso-MasterMusic.md) · [Diagrama de semana 1](Documentacion/Diagrama%20CasoDeUso.drawio.png) |
| Arquitectura | [Documento](Documentacion/Arquitectura-MasterMusic.md) |
| Clases | [Diagrama](Documentacion/Diagrama-clases-MasterMusic.md) |
| Datos de ambas APIs | [Modelo entidad–relación](Documentacion/Modelo-entidad-relacion-MasterMusic.md) |
| Interacciones | [Tres diagramas de secuencia](Documentacion/Diagramas-secuencia-MasterMusic.md) |
| Despliegue | [Documento](Documentacion/Diagrama-despliegue-MasterMusic.md) · [PNG](Documentacion/Diagrama-despliegue-MasterMusic.png) · [Fuente Mermaid](Documentacion/Diagrama-despliegue-MasterMusic.mmd) |
| Estados del pedido | [Documento](Documentacion/Diagrama-estados-pedido-MasterMusic.md) · [PNG](Documentacion/Diagrama-estados-pedido-MasterMusic.png) · [Fuente Mermaid](Documentacion/Diagrama-estados-pedido-MasterMusic.mmd) |
| Contratos de diseño de semana 2 | [Catálogo](Documentacion/Contratos/mastermusic-api.openapi.json) · [Pedidos](Documentacion/Contratos/mastermusic-ventas-api.openapi.json) · [Notas](Documentacion/Contratos/README.md) · [Cambios](Documentacion/Contratos/CAMBIOS.md) |
| Trazabilidad | [Matriz RF, HU, endpoints y pruebas](Documentacion/Matriz-trazabilidad-MasterMusic.md) |
| Decisiones de arquitectura | [ADR 001](Documentacion/ADR/ADR-001-dos-apis-y-bases-independientes.md) · [ADR 002](Documentacion/ADR/ADR-002-jwt-roles-y-clave-interna.md) · [ADR 003](Documentacion/ADR/ADR-003-idempotencia-y-stock.md) |
| Cliente | [Guía de React](MasterMusic.Client/README.md) |

Los contratos estáticos registran el diseño de semana 2. Los enlaces OpenAPI del inicio de este README permiten inspeccionar los endpoints generados por la implementación que se está ejecutando.

Evidencias funcionales de semana 3:

- [01 — Pedido cancelado](Documentacion/Evidencias/Semana-3/01-pedido-cancelado.png).
- [02 — Stock tras cancelar](Documentacion/Evidencias/Semana-3/02-stock-tras-cancelacion.png).
- [03 — Pedido confirmado](Documentacion/Evidencias/Semana-3/03-pedido-confirmado.png).
- [04 — Stock tras confirmar](Documentacion/Evidencias/Semana-3/04-stock-tras-confirmacion.png).
- [05 — Swagger de pedidos](Documentacion/Evidencias/Semana-3/05-swagger-api-pedidos.png).
- [06 — Swagger del catálogo](Documentacion/Evidencias/Semana-3/06-swagger-api-catalogo.png).

## Problemas habituales

| Síntoma | Comprobación |
| --- | --- |
| La consola queda en `Application started` | Es normal: la API está funcionando. Usa otra ventana para otro servicio. |
| `node` o `npm.cmd` no se reconocen | Comprueba la instalación de Node y abre una nueva ventana para cargar su PATH. |
| Error de conexión SQL o tablas inexistentes | Comprueba el servicio SQL, la instancia, la conexión correspondiente y las migraciones de esa API. |
| Falta `ConnectionStrings__MasterMusicVentas` tras reiniciar | Guarda la configuración como variable de usuario y abre una nueva terminal. Una variable definida solo con `$env:...` dura en esa ventana. |
| Error de certificado al acceder a las APIs | Ejecuta `dotnet dev-certs https --trust` en tu cuenta de Windows y reinicia los servicios. |
| Inicio de sesión devuelve `401` | Comprueba correo y contraseña de una cuenta existente y activa. Cambiar la variable de creación del administrador no cambia una contraseña almacenada. |
| Login funciona pero pedidos responde `401` | Comprueba que ambas APIs compartan `Jwt__Key`, `Jwt__Issuer` y `Jwt__Audience`; reinicia e inicia sesión otra vez. |
| Respuesta `403` | Usa el rol exigido por la ruta: Cliente para pedidos propios y Administrador para administración. |
| El catálogo abre pero no se puede crear un pedido | Comprueba la API de pedidos, la URL HTTPS de inventario y que ambas APIs compartan `Inventario__ServiceKey`. |
| CORS o puerto 5173 ocupado | Mantén el origen exacto configurado. Si cambias el puerto de React, ajusta CORS en ambas APIs. |
| Compilación con archivos en uso | Detén las APIs con Ctrl+C y vuelve a compilar. |
| Pedido con resultado incierto | Conserva la clave y utiliza Consultar resultado con la misma cuenta. |

La ejecución descrita corresponde al entorno local de desarrollo. Para publicar se requieren direcciones reales, HTTPS válido, configuración de secretos y CORS, y alojamiento para las dos APIs, las bases y los archivos `dist` de React.
