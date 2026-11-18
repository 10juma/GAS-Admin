# GAS-Admin

Panel de control interno de **Global App Suite**: una sola aplicación para operar la empresa y su portafolio de
productos SaaS (**Revendo, Pitazo, RentaFest, VecindarioDigital y Togado**). Reúne dirección, productos, clientes,
ventas, proyectos, finanzas, infraestructura, atención al cliente, equipo y seguridad.

> **Estado:** todo el mapa funcional está construido (fases 1 a 7 y Dirección). Lo pendiente es de puesta en marcha:
> capturar las llaves de los servicios externos, implementar la API GAS v1 en cada producto, publicar con HTTPS y
> capturar los datos reales de la empresa. Ver [Pendientes](#pendientes).

---

## Contenido

- [Tecnología](#tecnología)
- [Módulos](#módulos)
- [Roles y permisos](#roles-y-permisos)
- [Configuración y llaves](#configuración-y-llaves)
- [Desarrollo local](#desarrollo-local)
- [Producción](#producción)
- [Procesos automáticos](#procesos-automáticos)
- [Endpoints públicos](#endpoints-públicos)
- [Seguridad](#seguridad)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Pendientes](#pendientes)
- [Documentación adicional](#documentación-adicional)

---

## Tecnología

| Pieza | Uso |
|---|---|
| **.NET 10 · ASP.NET Core Razor Pages** | Aplicación web (servidor), páginas con modales para crear y editar. |
| **Entity Framework Core 10 + SQL Server** | Base de datos `GAS` (remota). Migraciones en `Data/Migrations`. |
| **ASP.NET Core Identity** | Usuarios propios de GAS-Admin, roles, bloqueo por intentos, recuperación de contraseña. |
| **QuestPDF** (licencia Community) | PDF de cotizaciones. |
| **Resend** (API HTTP) | Correo saliente (cotizaciones, soporte, contraseñas) y correo entrante de soporte (webhook). |
| **Stripe, UptimeRobot, GitHub, Sentry** | Integraciones de solo lectura vía HTTP, sincronizadas en segundo plano. |
| **Bootstrap 5 + CSS propio** (`wwwroot/css/gas.css`) | Interfaz; sin frameworks de JavaScript (`wwwroot/js/site.js`). |

Los movimientos automáticos (cotizaciones aprobadas, renovaciones, Stripe) se asignan a una categoría financiera si
existe una con el nombre esperado (`Services/Finanzas/CategoriasSistema.cs`: "Suscripciones SaaS", "Desarrollo y
proyectos", "Servicios recurrentes", "Servicios en la nube y software", "Dominios y hosting", "Comisiones bancarias y
de pago"); si no existe, el movimiento queda sin categoría.

Toda la interfaz está en español y con formatos de México (`es-MX`, horario del centro de México).

---

## Módulos

El menú lateral se agrupa por secciones (acordeón, contraíble a solo íconos). La definición única del menú está en
`Services/Navegacion/MenuPrincipal.cs`.

| Sección | Pantallas | Qué hace |
|---|---|---|
| **General** | Dashboard | Indicadores rápidos, próximas renovaciones, actividad reciente y equipo. |
| **Dirección** | Tablero de dirección | Indicadores ejecutivos de finanzas, comercial, producto y operación; cierre mensual automático (solo un Admin reescribe cierres). |
| **Productos** | Catálogo, Conexiones, Cuentas | Portafolio de productos; conexión con el super-admin de cada producto (API GAS v1): estado, métricas, cuentas (tenants), acciones (suspender, reactivar, extender prueba, reenviar acceso) y acceso firmado al super-admin. |
| **Comercial** | Clientes, Oportunidades, Cotizaciones, Catálogo comercial | CRM ligero: Kanban de oportunidades, cotizaciones con folio `COT-AAAA-NNNN`, versiones, PDF, envío por correo; al aprobarse crean el trabajo en Proyectos. |
| **Proyectos** | Trabajos, Tablero de tareas, Registro de horas | Trabajos (`TRB-AAAA-NNNN`) con entregables, ciclos mensuales, rentabilidad (horas × costo/hora), archivos, tareas en Kanban y horas por semana. |
| **Finanzas** | Resumen, Suscripciones SaaS, Movimientos, Configuración | Cobros y pagos (con IVA y tipo de cambio), cuentas, categorías, Stripe (MRR/ARR, churn, pagos fallidos; cada factura pagada entra como ingreso y su comisión como egreso). |
| **Infraestructura** | Panel, Ambientes, Repositorios, Dominios, Servicios externos, Incidentes (`INC-AAAA-NNNN`), Credenciales, Errores, Integraciones | Estado técnico por producto, despliegues, vencimientos (dominios, SSL, credenciales, servicios), incidentes automáticos desde UptimeRobot, releases de GitHub como despliegues, errores de Sentry. |
| **Atención al cliente** | Tickets (`TKT-AAAA-NNNN`), Base de conocimientos | Bandeja con SLA, respuestas por correo, notas internas, correo entrante que crea o continúa tickets, tickets desde los productos, artículos reutilizables. |
| **Organización** | Equipo, Departamentos y puestos, Datos de la empresa, **Configuración** | Personas (socios, empleados, externos) con capacidad y costo/hora; datos formales para PDF y correos; configuración y llaves de GAS-Admin (solo Admin). |
| **Administración** | Usuarios, Roles y permisos, Auditoría | Usuarios internos, permisos por módulo, bitácora completa de accesos y cambios. |

**Acceso:** inicio de sesión con "Recordarme" (30 días) y "¿Olvidaste tu contraseña?" con enlace de un solo uso
por correo (30 minutos).

---

## Roles y permisos

Cada módulo tiene permisos `Ver` y, si aplica, `Gestionar` (`Security/Permisos.cs`). Roles iniciales
(`Security/RolesBase.cs`), editables desde **Administración › Roles y permisos**:

| Rol | Alcance por defecto |
|---|---|
| **Admin** | Todo. Es el único que puede: dar o quitar el rol Admin, conectar productos o cambiar sus URLs y claves, capturar llaves y secretos, y reescribir cierres de Dirección. |
| **Desarrollador** | Productos, Proyectos, Infraestructura y Soporte (lectura). |
| **Finanzas** | Finanzas, Comercial, Proyectos y Dirección. |
| **Soporte** | Atención al cliente (gestiona), Comercial, Productos y Proyectos (lectura). |

Reglas adicionales: nadie puede otorgar a un rol permisos que no tiene; los montos y el costo por hora solo se ven
con `Finanzas.Ver` (también se ocultan en la bitácora a quien no lo tiene).

---

## Configuración y llaves

GAS-Admin **no usa user-secrets**. Hay tres niveles:

### 1. Fuera de la base de datos (solo dos datos)

No pueden vivir en la base de datos que protegen:

| Dato | Desarrollo | Producción |
|---|---|---|
| **Conexión a la BD** | `appsettings.Development.json` (excluido de git) | Variable `ConnectionStrings__DefaultConnection` |
| **Llave maestra** (cifra las llaves de la BD) | Se genera sola la primera vez en `~/.gas-admin/llave-maestra.key` (permisos 600) | Variable `Seguridad__LlaveMaestra` (32 bytes en Base64) o el archivo `~/.gas-admin/llave-maestra.key` (ruta configurable con `Seguridad__ArchivoLlaveMaestra`). En producción **no** se genera sola. |

> **Respalda la llave maestra.** Si se pierde, las llaves guardadas no se pueden leer y hay que volver a capturarlas.

### 2. En la base de datos, cifradas (las captura un Admin en pantalla)

Se guardan en la tabla `LlavesConfiguracion` con **AES-256-GCM**. Las llaves secretas solo se escriben (después se
ven solo sus últimos 4 caracteres), entran en uso **sin reiniciar** y cada cambio queda en la auditoría sin el valor.

| Pantalla | Llaves |
|---|---|
| **Organización › Configuración** | Resend: API key y remitente. Soporte: correo de respuesta (Reply-To) y secreto del webhook de correo entrante. Dirección pública de GAS-Admin (`https://…`). Incluye vista previa de los correos y "Enviarme un correo de prueba". |
| **Finanzas › Configuración › Stripe** | API key **restringida** de solo lectura por producto (Customers, Subscriptions, Invoices, Charges, Balance). |
| **Infraestructura › Integraciones** | UptimeRobot (Read-Only API Key), GitHub (token fine-grained de solo lectura), Sentry (auth token y organización). |
| **Productos › Conexiones** | Secreto compartido de cada producto (se genera en el navegador; va también en el producto como `GAS_SECRETO`). |

El catálogo de qué se puede capturar está en `Services/Configuracion/CatalogoLlaves.cs`. Si una llave también existe
como variable de entorno, gana la capturada en pantalla.

### 3. En `appsettings.json` (sin secretos)

Niveles de registro (SQL de EF en `Warning`), `AllowedHosts` y valores por defecto. Opcionales por variable de entorno:
`Archivos__Ruta` (carpeta de archivos de proyectos; por defecto `App_Data/archivos`), intervalos de sincronización
(`Stripe__MinutosEntreSincronizaciones`, `Integraciones__MinutosEntreSincronizaciones`,
`Productos__MinutosEntreSincronizaciones`) y `…__SincronizacionAutomatica=false` para apagarlas.

---

## Desarrollo local

Requisitos: **.NET 10 SDK**, acceso a la base `GAS` y la herramienta de EF (`dotnet tool install -g dotnet-ef`).

1. Crea `appsettings.Development.json` (no se sube a git) con la conexión:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=…;Database=GAS;User Id=…;Password=…;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
     },
     "DetailedErrors": true
   }
   ```

2. Aplica las migraciones:

   ```bash
   dotnet ef database update
   ```

3. Arranca la aplicación (https://localhost:7292):

   ```bash
   dotnet run --launch-profile https
   ```

   La llave maestra se crea sola en `~/.gas-admin/llave-maestra.key`. Al arrancar solo se siembran los roles y sus
   permisos (si faltan); productos, servicios externos, departamentos, categorías financieras, SLA y datos de la
   empresa se capturan desde la aplicación.

4. **Primer administrador** (solo si la base es nueva): arranca una vez con las variables
   `SeedAdmin__Email`, `SeedAdmin__Password` (y opcional `SeedAdmin__Nombre`) en ambiente Development; después
   quítalas y cambia la contraseña desde el menú de usuario.

5. Sin Resend configurado, en Development el enlace de "¿Olvidaste tu contraseña?" se escribe en la consola del
   servidor para poder probar el flujo.

---

## Producción

La lista completa está en **[docs/publicacion.md](docs/publicacion.md)**. Resumen:

### Variables de entorno

| Variable | Obligatoria | Nota |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT=Production` | Sí | Activa HSTS, página de error, rechaza IPs privadas en conexiones con productos y no genera la llave maestra. |
| `ConnectionStrings__DefaultConnection` | Sí | Usuario de SQL con permisos solo sobre la base `GAS`. |
| `Seguridad__LlaveMaestra` | Sí | `openssl rand -base64 32`. Distinta a la de desarrollo. **Respaldarla.** |
| `AllowedHosts` | Sí | El dominio definitivo (hoy `*` porque aún no se define). Varios con `;`. |
| `Archivos__Ruta` | Recomendada | Carpeta privada con respaldo para los archivos de proyectos. |

### Antes de abrirla al equipo

1. HTTPS con certificado válido (la cookie de sesión es `Secure`).
2. Aplicar migraciones: `dotnet ef database update` o un script idempotente (`dotnet ef migrations script --idempotent`).
3. Crear el primer Admin (ver [Desarrollo local](#desarrollo-local), paso 4) si la base es nueva.
4. Como Admin, en **Organización › Configuración**:
   - **Dirección pública** (`https://admin.tudominio.com`): sin ella, en producción no se envían enlaces de recuperación de contraseña y los correos no muestran el logo.
   - **Resend** (API key y remitente de un dominio verificado) → botón **Enviarme un correo de prueba**.
   - **Soporte**: correo de respuesta y secreto del webhook; en Resend crea el webhook `email.received` apuntando a `https://<dominio>/webhooks/resend/correo-entrante` ([guía](docs/soporte-correo-entrante.md)).
5. Capturar las llaves de **Stripe** (por producto), **UptimeRobot/GitHub/Sentry** y los **secretos de cada producto**, y lanzar una sincronización manual en cada pantalla.
6. Completar **Organización › Datos de la empresa** (aparecen en PDF y en el pie de los correos).
7. Respaldos programados de la base, de la carpeta de archivos y de la llave maestra.

---

## Procesos automáticos

Corren dentro de la aplicación (`BackgroundService`); cada uno evita ejecuciones simultáneas y lee las llaves vigentes.

| Proceso | Frecuencia | Qué hace |
|---|---|---|
| `SincronizadorStripe` | 60 min | Suscriptores, suscripciones, facturas y MRR por producto; ingresos y comisiones a Finanzas. |
| `SincronizadorIntegraciones` | 15 min | Monitores de UptimeRobot (abre y resuelve incidentes), repos y releases de GitHub, errores de Sentry. |
| `SincronizadorConexiones` | 15 min | Estado, métricas y cuentas de cada producto conectado (API GAS v1). |
| `CierreMensualAutomatico` | 6 h | Cierra el mes anterior en Dirección si no existe. |

Cada pantalla tiene además su botón **Sincronizar ahora**.

---

## Endpoints públicos

Sin sesión; protegidos por firma:

| Ruta | Uso | Protección |
|---|---|---|
| `POST /webhooks/resend/correo-entrante` | Correo entrante de soporte | Firma Svix con el secreto del webhook. |
| `POST /api/gas/v1/tickets`, `GET /api/gas/v1/tickets/{folio}` | Tickets desde los productos | HMAC con el secreto del producto + `X-GAS-Producto`. |
| `/Account/Login`, `/Account/RecuperarPassword`, `/Account/NuevaPassword` | Acceso | Antifalsificación; recuperación limitada por correo e IP. |

GAS-Admin también **consume** la API GAS v1 de cada producto (`/estado`, `/metricas`, `/cuentas`, acciones) con
firma HMAC-SHA256; contrato en [docs/api-gas-v1.md](docs/api-gas-v1.md).

---

## Seguridad

- Contraseñas: mínimo 10 caracteres con mayúscula, minúscula y número; bloqueo de 15 min tras 5 intentos; la sesión
  se revalida cada 5 minutos (cambios de permisos y desactivaciones aplican sin cerrar sesión).
- Recuperación de contraseña: nunca revela si un correo existe; enlace de un solo uso (30 min); los enlaces se arman
  solo con la dirección pública configurada (no con el host de la petición).
- Llaves de servicios cifradas en la BD; la llave maestra fuera de la BD. El inventario de **Credenciales** de
  Infraestructura solo guarda referencias (nunca valores) y `DetectorSecretos` rechaza valores que parezcan llaves en
  campos normales.
- Auditoría automática de altas, cambios y bajas (`Data/Interceptors/AuditoriaInterceptor.cs`) más eventos de acceso;
  montos ocultos a quien no tiene `Finanzas.Ver`; campos sensibles nunca se registran con su valor.
- Conexiones con productos: solo un Admin cambia URLs o claves; en producción se rechazan IPs privadas; el acceso al
  super-admin usa un token firmado de 60 s con audiencia.
- Folios (`COT`, `TRB`, `INC`, `TKT`) con reintento ante duplicados por concurrencia.

---

## Estructura del proyecto

```
Data/            DbContext, migraciones, semillas e interceptor de auditoría
Helpers/         Etiquetas, fechas (horario de México), íconos, avatares, detalle de auditoría
Infrastructure/  Toasts, filtro de errores de datos, fuente de configuración cifrada desde la BD
Middleware/      Cambio de contraseña obligatorio
Models/Entities/ Entidades por módulo
Pages/           Razor Pages por sección (cada lista con sus modales _XModal.cshtml)
Security/        Permisos, roles, cifrado, detector de secretos
Services/        Lógica por módulo, sincronizadores, correo (plantilla), configuración y llaves
docs/            API GAS v1, correo entrante de soporte, lista de publicación
wwwroot/         CSS, JS e imágenes
```

Convenciones de la interfaz: crear y editar en **modales con tarjetas**, ancho completo, chips con contorno, íconos,
spinners y protección contra doble clic. Los títulos de página toman su ícono del menú (`Pages/Shared/_IconoPagina.cshtml`).

---

## Pendientes

| Pendiente | Dónde |
|---|---|
| Definir el dominio y restringir `AllowedHosts` | Servidor / `docs/publicacion.md` |
| Publicar con HTTPS (necesario para correo entrante y logo en correos) | Servidor |
| Configurar Resend y probar el envío | Organización › Configuración |
| Capturar llaves de Stripe, UptimeRobot, GitHub y Sentry | Finanzas › Configuración, Infraestructura › Integraciones |
| Implementar la API GAS v1 en cada producto y conectar su secreto | Cada producto + Productos › Conexiones |
| Capturar datos reales: empresa, equipo y costos, ambientes, dominios, servicios externos | Organización, Infraestructura |
| Webhooks de Stripe (hoy es consulta periódica) | Cuando GAS-Admin esté publicado |

---

## Documentación adicional

- [docs/publicacion.md](docs/publicacion.md) — lista de verificación para producción.
- [docs/api-gas-v1.md](docs/api-gas-v1.md) — contrato que cada producto implementa (estado, métricas, cuentas, acciones, SSO firmado, tickets).
- [docs/soporte-correo-entrante.md](docs/soporte-correo-entrante.md) — configuración del correo entrante con Resend.
