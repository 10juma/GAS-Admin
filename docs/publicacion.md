# Publicación de GAS-Admin (lista de verificación)

Pendientes y ajustes para el ambiente de producción. Ningún secreto va en `appsettings.json`.
En el servidor solo se configuran la conexión y la llave maestra (variables de entorno, separador `__`);
las llaves de servicios externos las captura un Admin en la aplicación y quedan cifradas en la base de datos.

## 1. Dominio y AllowedHosts

`appsettings.json` trae `"AllowedHosts": "*"` porque el dominio definitivo aún no está decidido.
En cuanto se defina, restringirlo al dominio real (no dejar `*` en producción):

```
AllowedHosts=admin.ejemplo.com
```

Varios dominios se separan con `;`. Después de cambiarlo, comprobar que el login y el webhook
de Resend (`/webhooks/resend/correo-entrante`) responden en ese dominio.

## 2. Ambiente y HTTPS

- `ASPNETCORE_ENVIRONMENT=Production` (fuera de Development no se crea el administrador semilla,
  se activan HSTS y la página de error, y se rechazan IPs privadas en las conexiones con productos).
- Certificado HTTPS válido: la cookie de sesión es `Secure`.

## 3. Configuración y secretos

En desarrollo no hace falta user-secrets: la conexión va en `appsettings.Development.json` (excluido de git) y la
llave maestra se genera sola la primera vez en `~/.gas-admin/llave-maestra.key` (solo legible por tu usuario).

En el servidor solo se configuran estos datos (variables de entorno, separador `__`):

| Clave | Para qué |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server `GAS` (usuario con permisos solo sobre esa base). |
| `Seguridad__LlaveMaestra` | 32 bytes en Base64 (`openssl rand -base64 32`). Cifra todas las llaves capturadas en GAS-Admin. **Respáldala**: si se pierde, hay que volver a capturar las llaves. Usa una distinta a la de desarrollo. Alternativa: un archivo con la llave en `~/.gas-admin/llave-maestra.key` (o la ruta de `Seguridad__ArchivoLlaveMaestra`). En producción no se genera sola. |
| `Archivos__Ruta` | Carpeta privada (fuera de `wwwroot`) para los archivos de proyectos; con respaldo. |

Todo lo demás lo captura un **Admin** desde la aplicación; se guarda cifrado (AES-256-GCM) en la tabla
`LlavesConfiguracion`, entra en uso sin reiniciar y cada cambio queda en la auditoría (sin el valor):

| Dónde | Qué |
|---|---|
| Organización › Configuración | Resend (API key y remitente), correo de respuesta y secreto del webhook de soporte, dirección pública (`https://…`, obligatoria en producción para los enlaces de recuperación de contraseña). |
| Finanzas › Configuración › Stripe | API key restringida de cada producto. |
| Infraestructura › Integraciones | UptimeRobot, GitHub y Sentry. |
| Productos › Conexiones | Secreto compartido de cada producto (API GAS v1). |

Si una de esas llaves también existe como variable de entorno, gana la capturada en GAS-Admin.

## 4. Base de datos

- Aplicar las migraciones pendientes antes de arrancar (`dotnet ef database update` o script
  idempotente con `dotnet ef migrations script --idempotent`).
- Respaldo programado de la base y de la carpeta de archivos.

## 5. Registros

`appsettings.json` deja el SQL de Entity Framework en `Warning` para no llenar los registros con
cada consulta. Para diagnosticar se puede subir temporalmente con
`Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information`.

## 6. Después de publicar

- El administrador semilla (`SeedAdmin__Email`/`SeedAdmin__Password`) solo se crea en Development: si la
  base de producción es nueva, crearlo una vez corriendo la app en Development apuntando a ella con esas
  variables, cambiar su contraseña y no dejar `SeedAdmin` configurado en ningún lado. Revisar los roles.
- Entrar a Productos → Conexiones, Finanzas → Stripe e Integraciones y lanzar una sincronización manual.
- Enviar un correo de prueba al buzón de soporte y confirmar que se crea el ticket.
