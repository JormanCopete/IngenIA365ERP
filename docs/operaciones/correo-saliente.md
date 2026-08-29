# Correo saliente

> Para quien tenga que operarlo: qué servidor manda los correos, qué variables
> hay que definir, cómo comprobarlo antes de que haga falta, y por qué en local
> el correo **no llega a ningún buzón** (y eso es lo correcto).

Última revisión: 2026-08-19.

---

## 1. Qué correo manda el ERP

| Flujo | Disparador | Enlace que lleva dentro |
|---|---|---|
| Invitación a un tenant | Un admin invita a un correo | `{IdentityEmail:BaseUrl}/auth/accept-invitation?token=…` |
| Restablecimiento de contraseña | `POST /api/auth/forgot-password` | `{IdentityEmail:BaseUrl}/auth/reset-password?token=…` |
| Aviso de contraseña cambiada | Cambio de contraseña del propio usuario | — |
| Notificaciones de sistema | Bloqueo de cuenta y demás avisos | — |

Todos salen por la misma implementación: `SmtpEmailSender` (MailKit) en
`src/Infrastructure/IngenIA365ERP.Storage/Services/SmtpEmailSender.cs`, con
reintentos exponenciales 1 s / 4 s / 16 s más jitter.

> **Dos secciones de configuración, no una.** `Smtp` decide *por dónde sale* el
> mensaje; `IdentityEmail` decide *a dónde apunta el enlace que va dentro*. Se
> pueden romper por separado, y el segundo fallo es silencioso: el correo llega
> perfecto y el enlace no sirve.

> **La sección `EmailSender` no existe.** Hubo una en `appsettings` que ningún
> código leía; se eliminó en agosto de 2026. Si encontrás `EmailSender__Smtp__…`
> en una guía, una nota o un manifiesto de despliegue, está muerto: no configura
> nada y no falla de forma visible — simplemente se ignora.

---

## 2. Los dos destinos

### 2.1 Desarrollo — smtp4dev (captura, **no entrega**)

El destino por defecto en desarrollo es **smtp4dev**, un servidor SMTP falso que
corre en Docker (`docker-compose.dev.yml`, servicio `smtp4dev`):

| Qué | Dónde |
|---|---|
| Puerto SMTP | `localhost:1025` |
| Bandeja web | **http://localhost:8025** |

smtp4dev **acepta el mensaje y lo guarda**; nunca lo reenvía a internet. Por eso:

> **"No me llega el correo" es el comportamiento esperado en local.** El correo
> se mandó, y está en `http://localhost:8025`. No hay nada que arreglar. Este
> malentendido ya costó tiempo más de una vez: antes de tocar configuración,
> abrí la bandeja.

El `docker-compose.yml` clásico trae **MailHog** en lugar de smtp4dev, en los
mismos puertos (1025 SMTP, 8025 UI) y con exactamente el mismo comportamiento:
captura y no entrega.

En desarrollo el host y el puerto no se escriben a mano: se eligen con el
selector `Infraestructura:Destinos:Smtp` de `appsettings.Development.json`, que
acepta tres valores:

| Destino | Host / puerto | Entrega de verdad |
|---|---|---|
| `Local` | `127.0.0.1:25` | Depende de lo que tengas escuchando ahí |
| `Docker` *(default)* | `127.0.0.1:1025` → smtp4dev | **No** |
| `Microsoft365` | `mail.notifica365.com:587` | **Sí** |

> El destino se llama `Microsoft365` por razones históricas. **No es Microsoft
> 365**: es un Poste.io/Haraka autoalojado. El nombre miente; el host no.

### 2.2 Real — mail.notifica365.com

| Qué | Valor |
|---|---|
| Host | `mail.notifica365.com` |
| Puerto | `587` |
| Cifrado | STARTTLS (`Smtp__UseStartTls=true`) |
| Remitente | `noresponder.ingenia365erp@notifica365.com` |
| Nombre del remitente | `No Responder IngenIA365 ERP` |
| Usuario | el mismo buzón del remitente |
| Contraseña | **nunca en el repositorio** — variable de entorno o Secret |

Es el destino que usa `appsettings.Production.json`.

> **El servidor presenta un certificado TLS autofirmado de fábrica.** Es un
> Poste.io/Haraka autoalojado al que nunca se le puso un certificado válido, así
> que la validación contra las autoridades del sistema falla. Lo correcto es
> arreglar el certificado del servidor. Mientras tanto, ver §5.

---

## 3. Variables

Todas se pueden dar por variable de entorno con el separador `__` (doble guion
bajo), que es lo que se usa en contenedores y en Kubernetes.

### 3.1 Sección `Smtp` — por dónde sale

| Variable | Obligatoria | Notas |
|---|---|---|
| `Smtp__Host` | sí en prod | `mail.notifica365.com` |
| `Smtp__Port` | sí en prod | `587` |
| `Smtp__UseStartTls` | sí en prod | `true`. Con `false` MailKit negocia en modo `Auto` |
| `Smtp__Username` | sí en prod | El buzón completo, con dominio |
| `Smtp__Password` | sí en prod | **Solo** por entorno o Secret. Jamás en git |
| `Smtp__FromAddress` | sí | `noresponder.ingenia365erp@notifica365.com` |
| `Smtp__FromName` | no | `No Responder IngenIA365 ERP` |
| `Smtp__MaxRetries` | no | Default `3` (4 intentos en total) |
| `Smtp__InitialBackoffSeconds` | no | Default `1`; los reintentos crecen ×4 |
| `Smtp__HuellaCertificadoAceptada` | solo si hace falta | Ver §5 |

Si `Smtp__Username` viene vacío, el emisor **no autentica** — que es justo lo que
se quiere contra smtp4dev, y lo que hace que un despliegue real al que se le
olvidó el Secret falle con un rechazo del servidor en vez de arrancar mal.

### 3.2 Sección `IdentityEmail` — a dónde apunta el enlace

| Variable | Obligatoria | Notas |
|---|---|---|
| `IdentityEmail__BaseUrl` | **sí, en todos los ambientes** | Sin barra final |
| `IdentityEmail__InvitationLifetimeDays` | no | Default `7` |

Valores por ambiente:

| Ambiente | `IdentityEmail__BaseUrl` |
|---|---|
| Desarrollo (local) | `http://localhost:5200` |
| `erp-dev` | `https://app-dev.ingenia365.com` |
| `erp-qa` | `https://app-qa.ingenia365.com` |
| `erp-pdn` | `https://app.ingenia365.com` |

> **Si no la definís, el valor cableado por defecto es `https://localhost:7200`**,
> y ese enlace sale dentro de cada invitación y cada restablecimiento, desde
> cualquier ambiente, producción incluida. El correo se entrega sin error; lo que
> no funciona es el enlace, y sólo se entera quien lo recibe. Es el fallo más
> caro de esta página: comprobalo en cada despliegue nuevo.

---

## 4. Comprobarlo antes de que haga falta

`tools/scripts/probar-smtp.ps1` separa los cuatro fallos posibles y se detiene
en el primero, para que no haya que adivinar cuál fue:

1. resuelve el nombre y abre el puerto → descarta DNS y firewall de salida;
2. negocia STARTTLS y mira el certificado → descarta TLS (y dice la huella);
3. autentica → descarta credenciales;
4. envía un correo de prueba → descarta permisos de "enviar como".

La contraseña se pide por consola con `Read-Host -AsSecureString`: no viaja por
la línea de comandos, no queda en el historial y no se escribe en ningún archivo.

```powershell
# Todo el recorrido, con envío real a un buzón tuyo
.\tools\scripts\probar-smtp.ps1 -Destinatario alguien@ejemplo.com

# Solo red + TLS, sin pedir contrasena y sin mandar nada
.\tools\scripts\probar-smtp.ps1 -SoloDiagnostico

# Contra otro servidor o buzón
.\tools\scripts\probar-smtp.ps1 -Servidor otro.servidor.com -Puerto 587 -Usuario buzon@dominio.com
```

No necesita instalar nada: habla SMTP directamente sobre `TcpClient`, en vez de
usar MailKit, porque los paquetes de MailKit apuntan a .NET moderno y Windows
PowerShell 5.1 no puede cargarlos.

Cómo leer los fallos:

| Lo que dice | Qué pasó |
|---|---|
| No resuelve el nombre | DNS, o el host está mal escrito |
| El puerto no responde en 10 s | Firewall de salida, o el proveedor bloquea el 587 |
| `535` / `5.7.x` / *authentication* | Contraseña equivocada, MFA en el buzón, o SMTP básico desactivado |
| Error de certificado o TLS | El certificado autofirmado (§5) |
| Autentica pero no deja enviar | El buzón no tiene permiso de "enviar como" ese remitente |

Para que el ERP en local use el servidor real: poné `"Smtp": "Microsoft365"` en
`Infraestructura:Destinos` de `appsettings.Development.json` y pasá las
credenciales por entorno al arrancar (`$env:Smtp__Username`, `$env:Smtp__Password`).
Con el destino en `Docker` los correos siguen quedando en smtp4dev, que es lo que
conviene para el día a día.

---

## 5. El certificado autofirmado

El servidor presenta un certificado de fábrica que no valida contra ninguna
autoridad. Hay dos formas de convivir con eso, y sólo una es aceptable:

- **Desactivar la validación** — acepta *cualquier* certificado. Quien se
  interponga en la red puede leer los enlaces de invitación y de
  restablecimiento que van en esos correos. **No se hace.**
- **Fijar la huella** (`Smtp__HuellaCertificadoAceptada`) — acepta ese
  certificado y ninguno más. Si el servidor cambia de certificado, deja de
  conectar y nos enteramos. Es un parche, pero es el menos malo.

Obtener la huella SHA-256 sin separadores:

```bash
openssl s_client -starttls smtp -connect mail.notifica365.com:587 -showcerts </dev/null 2>/dev/null \
  | openssl x509 -noout -fingerprint -sha256 \
  | sed 's/.*=//; s/://g'
```

Ese valor va en `Smtp__HuellaCertificadoAceptada`. **No es un secreto** — es una
huella pública — así que puede ir en el ConfigMap o en el `appsettings` del
ambiente, no en el Secret. Si se deja vacío, sólo se aceptan certificados que
validen contra las autoridades del sistema, que es lo normal y lo deseable.

La solución de verdad es ponerle al servidor un certificado válido y borrar esta
variable.

---

## 6. El Secret en Kubernetes

El clúster es k3s (namespaces `erp-pdn`, `erp-dev`, `erp-qa`). Sólo usuario y
contraseña son secretos; host, puerto, TLS y remitente ya están en el
`configMapGenerator` de `workloads/erp/base/kustomization.yaml`, y
`IdentityEmail__BaseUrl` en el overlay de cada ambiente.

> **Las claves del Secret se llaman `Smtp__Username` y `Smtp__Password`**, igual
> que las variables — y **no** `username` / `password`, que es la convención de
> los otros Secrets del clúster (`erp-db-app`, `erp-master-admin`). Las lee el
> `secretKeyRef` de `workloads/erp/base/api.yaml`. Crear el Secret con las claves
> de la otra convención **no da ningún error**: el `secretKeyRef` es
> `optional: true`, el pod arranca igual, el emisor se queda sin usuario y no
> autentica. Es el fallo silencioso más fácil de provocar en esta página.

### 6.1 Crearlo

Se crea **en el servidor**, a mano, una vez. La contraseña se lee por consola y
se pasa por archivo — no como argumento — porque los argumentos de `kubectl` son
visibles en `ps` para cualquiera que esté en la máquina:

```bash
umask 077
read -rsp 'Contrasena del buzon de correo: ' SMTP_PASS; echo
printf '%s' "$SMTP_PASS" > /tmp/smtp-pass
unset SMTP_PASS

kubectl -n erp-pdn create secret generic erp-smtp \
  --from-literal=Smtp__Username=noresponder.ingenia365erp@notifica365.com \
  --from-file=Smtp__Password=/tmp/smtp-pass

shred -u /tmp/smtp-pass
```

Hay que crearlo en **cada** namespace que vaya a mandar correo (`erp-dev`,
`erp-qa`, `erp-pdn`): un Secret no se comparte entre namespaces.

Comprobar que quedó (sin mostrar el valor):

```bash
kubectl -n erp-pdn get secret erp-smtp -o jsonpath='{.data}' | tr ',' '\n'
```

> Si el Secret tiene que vivir en el repositorio de GitOps, va **cifrado con SOPS
> + age**, que es lo que usa este despliegue. Un manifiesto de Secret en claro no
> entra en git: base64 no es cifrado.

### 6.2 Consumirlo

Ya está hecho en el repositorio de GitOps; se documenta para saber dónde mirar
cuando falle. En `workloads/erp/base/api.yaml`, sólo las credenciales:

```yaml
            - name: Smtp__Username
              valueFrom: { secretKeyRef: { name: erp-smtp, key: Smtp__Username, optional: true } }
            - name: Smtp__Password
              valueFrom: { secretKeyRef: { name: erp-smtp, key: Smtp__Password, optional: true } }
```

El resto no es secreto y vive en el `configMapGenerator` de
`workloads/erp/base/kustomization.yaml`, igual para los tres ambientes:

```yaml
      - Smtp__Host=mail.notifica365.com
      - Smtp__Port=587
      - Smtp__UseStartTls=true
      - Smtp__FromAddress=noresponder.ingenia365erp@notifica365.com
      - Smtp__FromName=No Responder IngenIA365 ERP
```

Y lo único que cambia por ambiente, en el overlay correspondiente
(`workloads/erp/overlays/<ambiente>/kustomization.yaml`):

```yaml
      - IdentityEmail__BaseUrl=https://app.ingenia365.com   # pdn
```

### 6.3 Rotarlo

Cambiar un Secret **no** reinicia los pods: hay que forzar el despliegue.

```bash
umask 077
read -rsp 'Contrasena nueva: ' SMTP_PASS; echo
printf '%s' "$SMTP_PASS" > /tmp/smtp-pass
unset SMTP_PASS

kubectl -n erp-pdn create secret generic erp-smtp \
  --from-literal=Smtp__Username=noresponder.ingenia365erp@notifica365.com \
  --from-file=Smtp__Password=/tmp/smtp-pass \
  --dry-run=client -o yaml | kubectl apply -f -

shred -u /tmp/smtp-pass
kubectl -n erp-pdn rollout restart deployment/erp-api
```

Después de rotar, mandá una invitación de prueba a un buzón real: es la única
comprobación que cubre credenciales, TLS, permisos de envío y el enlace.

---

## 7. Diagnóstico

| Síntoma | Causa habitual | Qué hacer |
|---|---|---|
| En local no llega nada a mi buzón | Es lo esperado: smtp4dev captura y no entrega | Abrir `http://localhost:8025` |
| Tampoco aparece en `localhost:8025` | Host o puerto mal, o smtp4dev caído | `docker compose -f docker-compose.dev.yml ps`; confirmar `Smtp__Host`/`Smtp__Port` |
| El enlace del correo apunta a `localhost:7200` | Falta `IdentityEmail__BaseUrl` en ese ambiente | Definirla; §3.2 |
| `535` / *authentication failed* | Credenciales, o el Secret no llegó al pod | `probar-smtp.ps1`; revisar el `secretKeyRef` del pod |
| Error de certificado en el log | Certificado autofirmado del servidor | §5 |
| El puerto 587 no abre desde el clúster | Firewall de salida del proveedor | Probar desde el nodo antes de culpar a la app |
| Autentica pero rechaza el envío | El buzón no puede "enviar como" ese remitente | Alinear `Smtp__FromAddress` con `Smtp__Username` |
| Se configuró `EmailSender__…` y no cambió nada | Sección muerta, nadie la lee | Renombrar a `Smtp__…` / `IdentityEmail__…` |

Los reintentos y los fallos definitivos quedan en el log de la API
(`SmtpEmailSender`) y, al agotarse, en `COR_NotificationDeliveryFailures`.

---

## Referencias

- Configuración de destinos en dev: [`infraestructura-local.md`](infraestructura-local.md)
- Stack local completo: [`setup-local-pruebas.md`](setup-local-pruebas.md)
- Clúster, namespaces y política de secretos: [`despliegue-infraestructura.md`](despliegue-infraestructura.md)
- Script de comprobación: `tools/scripts/probar-smtp.ps1`
- Emisor: `src/Infrastructure/IngenIA365ERP.Storage/Services/SmtpEmailSender.cs`
- Opciones: `SmtpSettings.cs` (Storage) e `IdentityEmailOptions.cs` (Application)
