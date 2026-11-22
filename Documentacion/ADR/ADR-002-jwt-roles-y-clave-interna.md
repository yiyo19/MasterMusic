# ADR 002 Centralizar autenticación y comprobar permisos en cada API

**Estado:** aplicado. **Fecha del registro:** 22 de noviembre de 2026.
**Requisitos relacionados:** RF-01 a RF-03, RF-18, RF-19, RNF-01 a RNF-03 y RNF-08.

## Contexto

El cliente debe iniciar sesión una vez y consumir dos APIs. Los clientes solo pueden consultar sus pedidos; los administradores gestionan catálogo y pedidos. Una opción visible u oculta en React no constituye un control de autorización del servidor.

## Decisión

La primera API valida las credenciales con BCrypt y emite un JWT con identidad y rol. Ambas APIs validan firma, emisor, audiencia y vigencia. Las rutas protegidas declaran `Cliente` o `Administrador` según la operación.

La identidad de los pedidos y de sus operaciones se obtiene del token, no de un identificador de usuario enviado en el cuerpo. La consulta de un pedido ajeno no revela sus datos.

Las APIs comparten la configuración JWT mediante variables de entorno. Las rutas internas de inventario exigen además `X-Service-Key`, conocida solo por los servidores, para que disponer de un JWT de cliente no permita modificar stock directamente.

React conserva el JWT en memoria. Una respuesta 401 en una ruta protegida elimina la sesión y solicita un nuevo acceso; un 403 conserva una sesión válida e informa falta de permisos. Los datos necesarios para recuperar una operación se guardan por cuenta, separados del token.

## Alternativas consideradas

Confiar solamente en los controles visuales de React dejaría expuestas las rutas a solicitudes directas. Emitir identidades distintas en cada API complicaría la relación entre cliente y pedido. Guardar el JWT en almacenamiento persistente simplificaría recargas, pero prolongaría su presencia en el navegador.

## Consecuencias

Una recarga puede requerir iniciar sesión nuevamente. El cierre de sesión local deja de usar el token; no existe una lista de revocación inmediata en el servidor. Un JWT emitido conserva su validez hasta expirar si se presenta fuera de esa sesión.

Compartir una clave simétrica es una decisión del entorno académico: los servidores que la conocen deben protegerla. La clave interna nunca se incorpora a las variables públicas `VITE_`.

El registro público crea exclusivamente clientes. El administrador inicial se configura fuera del repositorio; modificar esa configuración no cambia la contraseña de una cuenta ya creada.

## Implementación y verificación

- [Emisión del JWT y acceso](../../MasterMusic.Api/Controllers/AuthController.cs).
- [Validación en pedidos](../../MasterMusic.Ventas.Api/Program.cs).
- [Consulta de pedidos propios](../../MasterMusic.Tests/PedidosConsultaTests.cs).
- [Validaciones de acceso](../../MasterMusic.Tests/AuthValidacionTests.cs).
- [Pruebas de sesión de React](../../MasterMusic.Client/tests/client.test.mjs).
- [Configuración](../../.env.example).

La autorización y el transporte de tokens no resuelven por sí solos la unicidad del correo; esa regla pertenece al registro y su persistencia.
