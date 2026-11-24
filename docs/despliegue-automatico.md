# Despliegue automático

GAS-Admin publica los productos sin usar la terminal: descarga el código de GitHub, lo compila **en el
servidor** con Docker, respalda la base, aplica migraciones, reemplaza los contenedores y verifica que
respondan. Si un contenedor no arranca, **todos** regresan solos a su imagen anterior.

## Cómo funciona

```
GAS-Admin ──(API de GitHub, token de solo lectura)──► código del commit (tar.gz)
    │
    └──(SSH con llave, huella verificada)──► servidor (usuario gasdeploy)
            1. sube y descomprime el código en ~/gas/<producto>/src
            2. docker build de cada ambiente (+ imagen de migraciones)
            3. archivos .env por contenedor (permisos 600)
            4. respaldo de la base (BACKUP … COPY_ONLY, 14 días) y migraciones (efbundle)
            5. docker rm -f + docker run de cada contenedor; espera a que responda
               (contenedor corriendo sin reinicios y la URL del ambiente < 500)
            6. si alguno falla: regresa todos a la imagen anterior
            7. limpia: conserva las 3 imágenes más recientes de cada una
```

- El servidor **no necesita** acceso a GitHub: GAS-Admin le sube el código.
- Se despliega de uno en uno (cola en segundo plano). La bitácora se ve en vivo en
  *Infraestructura › Servidores › Ver bitácora*; se puede cerrar la página y el despliegue sigue.
- Las migraciones ya aplicadas **no** se revierten (para eso está el respaldo previo).

## Seguridad

| Dato | Dónde queda |
|---|---|
| IP/dominio del servidor | `LlavesConfiguracion` (`Despliegue:Servidores:{id}:Host`), cifrado con la llave maestra. |
| Llave privada SSH (RSA 4096, generada por GAS-Admin) | `LlavesConfiguracion` (`Despliegue:Servidores:{id}:LlavePrivada`), secreto: nunca se muestra. |
| Contraseña de root | **No se guarda.** Se usa una sola vez en *Preparar* y se descarta. |
| Variables de cada contenedor (cadena de conexión, etc.) | `LlavesConfiguracion` (`Despliegue:Ambientes:{id}:Variables`), secreto: solo se muestran los nombres. |
| Huella del servidor | Tabla `Servidores`. Si cambia, GAS-Admin se niega a conectar. |

- Todo lo de despliegue es **solo para el rol Admin** y queda en la auditoría (preparación, inicio y fin de cada despliegue).
- `gasdeploy` está en el grupo `docker`: en la práctica eso equivale a administrador del servidor.
  La ventaja frente a guardar la contraseña de root es que la llave se puede revocar (Preparar de nuevo la
  reemplaza), está ligada a la huella del servidor y la contraseña nunca queda guardada.
- La contraseña de SQL para el respaldo viaja en un archivo temporal (600) y por variable de entorno, nunca
  en la línea de comandos.

## Puesta en marcha (una vez)

1. **Infraestructura › Integraciones › GitHub**: el token necesita permiso *Contents: read* sobre los repositorios a desplegar.
2. **Infraestructura › Repositorios**: el repositorio con su URL de GitHub; **Ambientes**: cada ambiente con su repositorio, rama y URL pública.
3. **Infraestructura › Servidores › Nuevo servidor**: nombre, IP y puerto SSH. Luego **Preparar** con `root` y su contraseña.
4. **Ambientes › Automatizar** en cada ambiente (ver valores de Pitazo abajo).
5. **Desplegar**.

## Valores para Pitazo

El repositorio de Pitazo trae `deploy/api.Dockerfile`, `deploy/web.Dockerfile`, `deploy/migraciones.Dockerfile`
y `.dockerignore`. Equivalen a los `docker run` que se hacían a mano.

| Campo | Pitazo API | Pitazo Web |
|---|---|---|
| Dockerfile | `deploy/api.Dockerfile` | `deploy/web.Dockerfile` |
| Contenedor / imagen | `pitazo-api` / `pitazo-api` | `pitazo-web` / `pitazo-web` |
| Puertos | `127.0.0.1:5003:8080` | `127.0.0.1:5002:8080` |
| Red | `pitazo-net` | `pitazo-net` |
| Volúmenes | `/root/pitazo-uploads:/app/wwwroot/uploads`<br>`/etc/pitazo/firebase-credentials.json:/app/firebase-credentials.json:ro`<br>`/root/pitazo-keys/api:/keys` | igual, con `/root/pitazo-keys/web:/keys` |
| Ruta para verificar | `/gas/v1/estado` (responde 401 sin firma: cuenta como vivo) | `/Account/Login` |
| Migraciones | Sí, `deploy/migraciones.Dockerfile`, con respaldo | No |
| Variables | `ASPNETCORE_ENVIRONMENT=Production`<br>`ConnectionStrings__DefaultConnection=Server=sqlserver;Database=Pitazo;User Id=sa;Password=…;TrustServerCertificate=True;MultipleActiveResultSets=true`<br>`Firebase__CredencialesJsonPath=/app/firebase-credentials.json` | las mismas |

`Gas__Secreto` se agrega solo (casilla *Agregar Gas__Secreto*) desde *Productos › Conexiones*.
El contenedor de SQL (`sqlserver`) y la base (`Pitazo`) para el respaldo se toman de la cadena de conexión.

## Si algo sale mal

- **"La huella del servidor cambió"**: el servidor se reinstaló o no es el mismo. Si es legítimo, *Preparar* de nuevo.
- **La compilación falla**: no se tocó nada; la bitácora muestra el error de `dotnet`/Docker.
- **Revertido**: un contenedor no respondió; la bitácora incluye sus últimas 60 líneas de log.
- **Interrumpido**: GAS-Admin se reinició a la mitad. Revisa en el servidor `docker ps` y vuelve a desplegar.
