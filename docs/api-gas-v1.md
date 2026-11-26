# API GAS v1 — contrato entre GAS-Admin y cada producto

Este documento describe lo que **cada producto** de Global App Suite (Revendo, Pitazo, RentaFest,
VecindarioDigital, Togado…) debe exponer para que GAS-Admin lo integre. GAS-Admin **nunca** se
conecta a la base de datos de un producto: todo pasa por esta API. El super-admin propio de cada
producto se conserva; esta API solo agrega una puerta estándar.

- Base: `https://<api-del-producto>/gas/v1` (la URL exacta se configura en GAS-Admin).
- Formato: JSON UTF-8, nombres en `camelCase`, fechas en ISO-8601 UTC (`2026-11-18T15:04:05Z`).
- Todas las peticiones van **firmadas** (ver abajo). Responder `401` si la firma no es válida.
- Tiempo de respuesta esperado: menos de 5 s por petición.

## 1. Autenticación: firma HMAC

Cada producto comparte con GAS-Admin un **secreto** (32 bytes aleatorios, en base64url). El secreto
nunca viaja en las peticiones y nunca se guarda en una base de datos: en GAS-Admin vive en
user-secrets / variables de entorno (`Productos__Conexiones__<clave>__Secreto`) y en el producto
en su propio gestor de secretos.

Cabeceras que envía GAS-Admin:

| Cabecera | Contenido |
|---|---|
| `X-GAS-Timestamp` | Segundos Unix en que se firmó. |
| `X-GAS-Firma` | `hex( HMAC-SHA256( secreto, cadena ) )` en minúsculas. |
| `X-GAS-Usuario` | Correo del usuario de GAS-Admin que hace la petición (o `sistema`). Úsalo para tu propia bitácora. |

`cadena` = `timestamp + "\n" + MÉTODO + "\n" + ruta_con_query + "\n" + hex(SHA-256(cuerpo))`

- `MÉTODO` en mayúsculas (`GET`, `POST`).
- `ruta_con_query` exactamente como llega, sin dominio: `/gas/v1/cuentas?pagina=2&porPagina=100`.
- Si no hay cuerpo, se usa el SHA-256 de la cadena vacía
  (`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`).

El producto debe:

1. Rechazar si `|ahora - timestamp| > 300` segundos (evita repeticiones).
2. Recalcular la firma y compararla en **tiempo constante**.
3. Responder `401 {"error":"firma inválida"}` si algo no coincide.

Ejemplo de validación en C# (ASP.NET Core):

```csharp
static bool FirmaValida(HttpRequest req, string cuerpo, byte[] secreto)
{
    if (!long.TryParse(req.Headers["X-GAS-Timestamp"], out var ts)) return false;
    if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300) return false;
    var hashCuerpo = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cuerpo))).ToLowerInvariant();
    var cadena = $"{ts}\n{req.Method.ToUpperInvariant()}\n{req.Path}{req.QueryString}\n{hashCuerpo}";
    var esperada = HMACSHA256.HashData(secreto, Encoding.UTF8.GetBytes(cadena));
    var recibida = Convert.FromHexString(req.Headers["X-GAS-Firma"].ToString());
    return CryptographicOperations.FixedTimeEquals(esperada, recibida);
}
```

## 2. `GET /gas/v1/estado`

Salud del producto. Debe ser rápido y no tocar datos sensibles.

```json
{
  "producto": "Revendo",
  "version": "1.8.2",
  "ambiente": "produccion",
  "ok": true,
  "dependencias": [
    { "nombre": "Base de datos", "ok": true, "detalle": "12 ms" },
    { "nombre": "Stripe", "ok": true },
    { "nombre": "Correo", "ok": false, "detalle": "Tiempo de espera agotado" }
  ],
  "hora": "2026-11-18T15:04:05Z"
}
```

`ok` es `false` si alguna dependencia crítica falla.

## 3. `GET /gas/v1/metricas`

Foto actual del uso del producto.

```json
{
  "cuentasTotales": 420,
  "cuentasActivas": 380,
  "cuentasEnPrueba": 25,
  "cuentasSuspendidas": 15,
  "usuariosTotales": 1900,
  "usuariosActivos24h": 610,
  "usuariosActivos30d": 1450,
  "altas30d": 32,
  "bajas30d": 6,
  "adicionales": [
    { "clave": "pedidos30d", "nombre": "Pedidos en 30 días", "valor": 15230, "unidad": "pedidos" },
    { "clave": "gmv30d", "nombre": "Ventas procesadas 30 días", "valor": 1840000.50, "unidad": "MXN" }
  ]
}
```

- "Cuenta" es la unidad que contrata: negocio, organización, vecindario, despacho, etc.
- `adicionales` es libre: cada producto reporta sus métricas propias (hasta 10).

## 4. `GET /gas/v1/cuentas?pagina=1&porPagina=100&modificadasDesde=2026-11-18T00:00:00Z`

Lista paginada de cuentas. `modificadasDesde` es opcional: si viene, devuelve solo las creadas o
modificadas desde esa fecha (GAS-Admin lo usa para sincronizar incrementalmente). `porPagina` máximo 200.

```json
{
  "pagina": 1,
  "totalPaginas": 5,
  "cuentas": [
    {
      "id": "acc_8f2k1",
      "nombre": "Abarrotes La Esperanza",
      "email": "contacto@laesperanza.mx",
      "plan": "Pro",
      "estado": "activa",
      "creadaUtc": "2026-02-10T18:00:00Z",
      "ultimoUsoUtc": "2026-11-18T14:55:00Z",
      "pruebaHastaUtc": null,
      "usuarios": 6,
      "stripeCustomerId": "cus_Q1w2e3r4"
    }
  ]
}
```

- `estado`: `activa`, `prueba`, `suspendida` o `cancelada`. Las cuentas canceladas **no se borran**
  de la API: se reportan como `cancelada` para que GAS-Admin lo sepa.
- `stripeCustomerId` (opcional) permite a GAS-Admin ligar la cuenta con su suscripción de Stripe.

## 5. `POST /gas/v1/cuentas/{id}/acciones`

Acciones administrativas básicas. GAS-Admin pide confirmación al usuario, exige permiso y registra
la acción en su auditoría; el producto también debe registrarla (con `X-GAS-Usuario`).

```json
{ "accion": "suspender", "motivo": "Falta de pago desde octubre" }
{ "accion": "reactivar", "motivo": "Pago recibido" }
{ "accion": "extender-prueba", "dias": 14, "motivo": "Demo con el dueño la próxima semana" }
{ "accion": "reenviar-acceso", "motivo": "No le llegó el correo de bienvenida" }
```

Respuesta `200`:

```json
{ "ok": true, "mensaje": "Cuenta suspendida.", "cuenta": { ...misma forma que en /cuentas... } }
```

Errores: `404` si la cuenta no existe, `409 {"ok":false,"mensaje":"La cuenta ya está suspendida."}`
si no aplica, `422` si falta un dato (por ejemplo `dias`). Una acción no soportada responde `400`.

## 6. Acceso al super-admin con token firmado (SSO)

El botón "Abrir super-admin" de GAS-Admin abre:

```
https://<super-admin-del-producto>/gas/sso?token=<token>
```

`token` = `base64url(carga) + "." + base64url( HMAC-SHA256( secreto, base64url(carga) ) )`

Carga (JSON):

```json
{ "aud": "admin.revendo.mx", "email": "persona@globalappsuite.com.mx", "nombre": "Nombre Apellido", "exp": 1795015200, "nonce": "a8f3…" }
```

El producto debe:

1. Validar la firma en tiempo constante.
2. Rechazar si `aud` no es exactamente el host del propio super-admin (así un token no sirve en otro sitio).
3. Rechazar si `exp` ya pasó (el token vive **60 segundos**).
4. Rechazar si el `nonce` ya se usó (guardar los usados al menos 2 minutos): **un solo uso**.
5. Abrir sesión en el super-admin con el usuario de ese `email` **solo si ya existe** como
   super-administrador del producto. No crear usuarios automáticamente.
6. Registrar el acceso en su bitácora.

## 7. Tickets de soporte hacia GAS-Admin

En este caso es el **producto** quien llama a GAS-Admin (el botón de ayuda dentro del producto).
Se firma igual que en la sección 1, con el mismo secreto, y se agrega la cabecera
`X-GAS-Producto: <clave>` (la clave con la que GAS-Admin registró al producto, ej. `revendo`).

`POST https://<gas-admin>/api/gas/v1/tickets`

```json
{
  "cuentaId": "acc_8f2k1",
  "nombre": "María López",
  "email": "maria@laesperanza.mx",
  "asunto": "No puedo imprimir el ticket de venta",
  "descripcion": "Desde ayer la impresora no responde al cobrar…",
  "prioridad": "alta",
  "categoria": "problema",
  "referencia": "ayuda-55231"
}
```

- Obligatorios: `asunto` y `descripcion`. `prioridad`: `baja`, `media` (por defecto), `alta`, `urgente`.
  `categoria`: `duda`, `problema`, `solicitud`, `facturacion`, `sugerencia`.
- `referencia` (opcional) es el id en el producto: si se repite, GAS-Admin devuelve el mismo ticket
  en lugar de crear otro (puedes reintentar sin duplicar).
- Respuesta `201 {"folio": "TKT-2026-0042", "id": 42}` (o `200` con `"existente": true`).
- Las respuestas del equipo le llegan al usuario por correo, con el folio en el asunto.

`GET https://<gas-admin>/api/gas/v1/tickets/{folio}` (firmado igual) devuelve el estado y la
conversación **visible para el cliente** (nunca las notas internas), para mostrarla dentro del producto:

```json
{ "folio": "TKT-2026-0042", "asunto": "…", "estado": "enespera", "actualizadoUtc": "…",
  "mensajes": [
    { "id": 120, "tipo": "cliente", "autor": "María López", "texto": "…", "fechaUtc": "…", "referencia": "msg-881" },
    { "id": 125, "tipo": "soporte", "autor": "Manuel Flores", "texto": "…", "fechaUtc": "…", "referencia": null } ] }
```

- `estado`: `nuevo`, `abierto`, `enespera` (esperando al cliente), `escalado`, `resuelto` o `cerrado`.
- `id` es único en GAS-Admin: guárdalo en el mensaje del producto para no importarlo dos veces.
- `referencia` es el id que el producto mandó al crear ese mensaje (ver abajo); `null` si se escribió
  en GAS-Admin o llegó por correo. Un mensaje con referencia tuya ya lo tienes.

Solo se pueden consultar y modificar tickets del mismo producto que firma.

### 7.1 Conversación sincronizada

El producto puede mostrar y continuar la conversación; lo que se escribe en un lado aparece en el otro.

`POST https://<gas-admin>/api/gas/v1/tickets/{folio}/mensajes`

```json
{ "tipo": "cliente", "texto": "Ya probé y sigue igual.", "autor": "María López",
  "email": "maria@laesperanza.mx", "referencia": "msg-881", "resolver": false }
```

- `tipo`: `cliente` (lo escribió el usuario en el producto) o `soporte` (lo escribió el equipo en el
  panel de soporte del producto). `texto` (máx. 8000) y `referencia` (máx. 100, id del mensaje en el
  producto) son obligatorios; repetir la referencia devuelve el mismo mensaje (`200`, `"existente": true`).
- Un mensaje del cliente reabre el ticket si estaba resuelto o esperando; en un ticket `cerrado`
  responde `409` (hay que abrir otro). Un mensaje de soporte lo deja esperando al cliente, o
  resuelto con `"resolver": true`. GAS-Admin no reenvía por correo lo que llega del producto.
- Respuesta `201 {"id": 125, "folio": "…", "estado": "enespera", "existente": false}`.

`POST https://<gas-admin>/api/gas/v1/tickets/{folio}/estado` con `{"estado": "resuelto"}`
(`abierto`, `resuelto` o `cerrado`) cuando el panel del producto cambia el estado.

**Aviso de cambios (GAS-Admin → producto).** Cuando en GAS-Admin se responde un ticket, cambia su
estado o el cliente contesta por correo, GAS-Admin llama al producto (firmado como en la sección 1):

`POST {urlApi}/tickets/sincronizar` con `{"folio": "TKT-2026-0042", "referencia": "<referencia del ticket>"}`

El producto responde `200` y trae la conversación con el `GET` de arriba, importando los mensajes
cuyo `id` aún no tiene. Es un aviso best-effort: el producto también debe sincronizar al abrir el
ticket. Si el producto no implementa la ruta (404), GAS-Admin lo ignora.

## 8. Versionado

Cambios compatibles (campos nuevos) se agregan sin cambiar `v1`. Un cambio incompatible será `v2`
y convivirá con `v1` mientras se migran los productos.
