# Migración de DNS a Cloudflare — `ingenia365.com`

> Guía paso a paso. Estado actual: nameservers en **GoDaddy** (`ns29/ns30.domaincontrol.com`).
> ⚠️ **El dominio tiene el correo corporativo en Microsoft 365.** Si un registro se pierde en la
> migración, el correo deja de funcionar. Por eso el paso 3 (verificación) no es opcional.

## 1. Inventario ANTES de migrar (levantado el 2026-08-09)

Estos son **todos** los registros que existen hoy. Cloudflare debe terminar con esta misma lista:

| Tipo | Nombre | Valor | Para qué sirve | Proxy en Cloudflare |
|---|---|---|---|---|
| A | `ingenia365.com` | `15.197.148.33` | Redirección web de GoDaddy | ❌ **se reemplaza** (ver §4) |
| A | `ingenia365.com` | `3.33.130.190` | Redirección web de GoDaddy | ❌ **se reemplaza** (ver §4) |
| CNAME | `www` | `ingenia365.com` | Alias del apex | 🟠 Proxied |
| **MX** | `ingenia365.com` | `0 ingenia365-com.mail.protection.outlook.com` | **Correo Microsoft 365** | ⚪ **DNS only (obligatorio)** |
| **TXT** | `ingenia365.com` | `v=spf1 include:spf.protection.outlook.com -all` | **SPF del correo** | ⚪ DNS only |
| **CNAME** | `autodiscover` | `autodiscover.outlook.com` | **Configuración automática de Outlook** | ⚪ **DNS only (obligatorio)** |
| A | `panel` | `85.239.231.94` | Panel en producción | ⚪ **DNS only al principio** (ver §4) |
| A | `app` | `85.239.231.94` | (reservado para el ERP) | ⚪ DNS only por ahora |

**No existen** (y conviene crearlos después): `_dmarc`, DKIM (`selector1/2._domainkey`), `CAA`, `api`.

> 🔴 **Regla de oro**: los registros de correo (**MX**, **SPF/TXT**, **autodiscover**, DKIM) van
> **siempre en gris (DNS only)**. Si los ponés en naranja (proxied), Cloudflare intenta
> proxear tráfico que no es HTTP y **el correo se rompe**.

## 2. Crear la zona en Cloudflare

1. Entrá a [dash.cloudflare.com](https://dash.cloudflare.com) y creá una cuenta (plan **Free**).
2. **Add a domain** → escribí `ingenia365.com` → **Continue**.
3. Elegí el plan **Free** → **Continue**.
4. Cloudflare escanea GoDaddy e importa los registros que encuentra. **Tarda ~1 minuto.**
5. Al final te muestra **2 nameservers** propios, con esta forma:
   ```
   xxxx.ns.cloudflare.com
   yyyy.ns.cloudflare.com
   ```
   Anotalos: son los del paso §5. **Todavía no cambies nada en GoDaddy.**

## 3. Verificar la importación (paso que NO se salta)

En Cloudflare, andá a **DNS → Records** y compará contra la tabla de §1.

Marcá especialmente:
- [ ] El **MX** de Outlook existe y está en **DNS only** (nube gris)
- [ ] El **TXT con `v=spf1 include:spf.protection.outlook.com -all`** existe
- [ ] El **CNAME `autodiscover`** existe y está en **DNS only**
- [ ] `panel` apunta a `85.239.231.94` en **DNS only**
- [ ] `www` existe

Si falta alguno, **agregalo a mano** con *Add record* antes de continuar.

## 4. Ajustes propios de la migración

**a) La redirección del apex.** Los dos registros A del apex (`15.197.148.33`, `3.33.130.190`)
son de la *redirección web* de GoDaddy, que **deja de existir** al cambiar los nameservers.
Se reemplaza por una regla nativa de Cloudflare:

1. Borrá esos dos registros A del apex.
2. Creá un registro **AAAA** `ingenia365.com` → `100::` en **Proxied** (naranja).
   *(Es la IP "descarte" de IPv6: no enruta a ningún lado, solo permite que Cloudflare
   procese la petición para aplicar la redirección.)*
3. Andá a **Rules → Redirect Rules → Create rule**:
   - Nombre: `apex a app`
   - Cuando: *Hostname* **equals** `ingenia365.com`
   - Entonces: *Dynamic redirect* → `concat("https://app.ingenia365.com", http.request.uri.path)`
   - Código: **301** · Preserve query string: ✅

**b) `panel` queda en gris al principio.** El Caddy del servidor renueva su certificado con
Let's Encrypt, y eso necesita alcanzar el puerto 80 directo. Cuando movamos el panel al
clúster con Cloudflare Tunnel, pasa a naranja.

**c) SSL/TLS.** En **SSL/TLS → Overview**, elegí **Full (strict)**. Nunca "Flexible"
(deja el tramo Cloudflare→servidor sin cifrar).

## 5. Cambiar los nameservers en GoDaddy

1. Entrá a [dcc.godaddy.com/domains](https://dcc.godaddy.com/domains) con tu cuenta.
2. Buscá `ingenia365.com` → **Domain Settings** (o los tres puntos → *Edit DNS*).
3. Bajá hasta **Nameservers** → **Change** / *Cambiar*.
4. Elegí **"I'll use my own nameservers"** / *Usaré mis propios servidores de nombres*.
5. Borrá `ns29.domaincontrol.com` y `ns30.domaincontrol.com`, y pegá los **dos de Cloudflare**
   del paso §2.5.
6. **Save**. GoDaddy puede pedirte confirmar por correo o mostrar una advertencia: aceptá.

## 6. Esperar y verificar

- La propagación tarda entre **5 minutos y 24 horas** (normalmente <1 h).
- Cloudflare te manda un correo *"ingenia365.com is now active"*.
- Verificación automática: pedile al asistente que corra el chequeo post-migración, que
  comprueba nameservers, correo (MX/SPF/autodiscover), panel y certificados.

## 7. Después de la migración (mejoras pendientes)

| Mejora | Por qué |
|---|---|
| **DMARC** (`_dmarc` TXT: `v=DMARC1; p=none; rua=mailto:...`) | Hoy **no existe**: cualquiera puede falsificar correos de `@ingenia365.com`. Crítico porque el ERP envía invitaciones y resets de contraseña |
| **DKIM** de Microsoft 365 | Tampoco existe; se habilita en el panel de M365 y crea dos CNAME |
| **CAA** (`0 issue "letsencrypt.org"` + `0 issuewild "letsencrypt.org"`) | Impide que otra autoridad emita certificados de tu dominio |

## 8. Si algo sale mal

**Reversa inmediata**: volvé a poner `ns29.domaincontrol.com` y `ns30.domaincontrol.com`
en GoDaddy. El DNS vuelve al estado anterior en minutos. Por eso **no se borra nada en
GoDaddy** durante la migración: la zona vieja queda intacta como respaldo.
