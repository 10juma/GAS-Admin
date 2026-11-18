# Correo entrante para Atención al cliente

GAS-Admin crea tickets con los correos que llegan al buzón de soporte (por ejemplo
`soporte@globalappsuite.com.mx`) y agrega al mismo ticket las respuestas de los clientes.
Usa **Resend Inbound** (el mismo proveedor con el que GAS-Admin envía correos).

## Cómo funciona

1. Resend recibe el correo y llama al webhook de GAS-Admin con el evento `email.received`.
2. GAS-Admin valida la firma del webhook (Svix) y, como el evento **no trae el cuerpo**, lo
   consulta con la API de Resend (`GET /emails/receiving/{id}`) usando `Resend:ApiKey`.
3. Si el asunto trae un folio `[TKT-AAAA-NNNN]`, el correo se agrega a ese ticket (y lo reabre si
   estaba esperando al cliente o resuelto). Si no trae folio, o el ticket estaba cerrado, se crea
   un ticket nuevo ligado al cliente o cuenta de producto con ese correo, si existe.
4. Se quita la parte citada del correo ("El … escribió:", líneas con `>`). Los correos repetidos
   (mismo Message-ID) se ignoran.

Las respuestas que se envían desde GAS-Admin llevan el folio en el asunto y `Reply-To` al buzón
de soporte, así la respuesta del cliente vuelve al mismo ticket.

## Configuración (requiere GAS-Admin publicado con HTTPS)

1. En Resend, agrega el dominio de recepción y crea el buzón (Inbound).
2. En Resend › Webhooks, crea un webhook a
   `https://<gas-admin>/webhooks/resend/correo-entrante` con el evento `email.received`
   y copia su secreto de firma (`whsec_…`).
3. Configura en GAS-Admin (user-secrets en desarrollo, variables de entorno en el servidor):

```bash
dotnet user-secrets set "Soporte:CorreoEntrante:SecretoWebhook" "whsec_…"
dotnet user-secrets set "Soporte:CorreoRespuesta" "soporte@globalappsuite.com.mx"
```

En el servidor: `Soporte__CorreoEntrante__SecretoWebhook` y `Soporte__CorreoRespuesta`.
También deben estar `Resend:ApiKey` (con permiso para leer correos recibidos) y `Resend:From`.
