# Canal digital — llaves y secretos: rotación y revocación (feature 021)

El canal (`canal/api`) y el ERP se hablan con tokens de servicio **ES256** de 60 segundos firmados por cada lado, y el canal cifra
las credenciales de la pasarela con una **KEK** (llave de cifrado de llaves). Ninguna de estas llaves va al repositorio (es
público) ni al CI: son Secrets del ambiente que carga el dueño. En desarrollo las genera `canal/scripts/llaves-dev.sh` fuera del
repositorio.

| Secreto | Dónde vive | Quién lo usa |
|---|---|---|
| Par ES256 del **canal** (privada) | Secret del canal: `CANAL_LLAVE_PRIVADA_FILE` + `CANAL_LLAVE_KID` | El canal firma hacia el ERP |
| Pública del canal | ERP: `CanalServicio:LlavesPublicas:{kid}` (una o varias) | El ERP verifica |
| Par ES256 del **ERP** (privada) | ERP: `CanalServicio:LlavePrivadaErp` + `CanalServicio:KidErp` | El ERP firma hacia el canal (`/internal/v1`) |
| Pública del ERP | Secret del canal: `CANAL_ERP_LLAVE_PUBLICA_FILE` | El canal verifica |
| KEK por versión | Secret del canal: `CANAL_KEK_V{n}_FILE` + `CANAL_KEK_ACTIVA` | Cifra el secreto de cada sitio de la pasarela |
| Secreto de la pasarela (por sitio) | Base del canal, cifrado con la KEK; **nunca** en el ERP | El canal firma con Placetopay |
| VAPID y cuenta de servicio de FCM | Secret del canal: `CANAL_VAPID_PRIVADA`, `CANAL_FCM_CUENTA_DE_SERVICIO` | Avisos por push |

## Rotar la llave del canal (canal → ERP) — sin corte

El ERP acepta varias públicas por `kid`.

1. Generar el par nuevo (`openssl ecparam -name prime256v1 -genkey -noout …`) con un `kid` nuevo (p. ej. `canal2`).
2. Agregar la pública al ERP en `CanalServicio:LlavesPublicas:canal2` **conservando** la anterior; desplegar el ERP.
3. Cambiar el Secret del canal a la privada nueva y `CANAL_LLAVE_KID=canal2`; reiniciar el canal.
4. Pasados unos minutos (los tokens viven 60 s), quitar la pública vieja del ERP.

## Rotar la llave del ERP (ERP → canal) — con una ventana corta

El canal verifica con **una** pública. Mientras dura el cambio, las llamadas del ERP al canal (credenciales, intentos para la
atención y la conciliación, reverso, indicadores) fallan y el ERP lo dice en pantalla; el pago en línea del asociado no se afecta.

1. Generar el par nuevo con `KidErp` nuevo.
2. Cambiar en el mismo momento el Secret del canal (`CANAL_ERP_LLAVE_PUBLICA_FILE`) y la privada del ERP; reiniciar los dos.
3. Repetir lo que haya fallado (un reverso se puede repetir: no reversa dos veces).

## Rotar la KEK

Cada sitio guarda la versión de la KEK con que se cifró su secreto (`gateway_sites.kek_version`).

1. Generar la KEK nueva (`openssl rand -base64 32`) y montarla como `CANAL_KEK_V2_FILE` **conservando** la v1; poner
   `CANAL_KEK_ACTIVA=2`; reiniciar el canal. Desde ahí, todo secreto nuevo se cifra con la v2 y los viejos se siguen leyendo con la v1.
2. Para pasar los secretos existentes a la v2, la cooperativa vuelve a enviar las credenciales de cada destino desde
   **Canal digital › Destinos de pago › Credenciales** (el canal las cifra con la activa).
3. Cuando ningún sitio tenga `kek_version = 1` (consulta en la base del canal), retirar `CANAL_KEK_V1_FILE`.

Perder una KEK que todavía cifra algún sitio deja esos sitios sin poder cobrar hasta que la cooperativa vuelva a enviar las credenciales.

## Cambiar el secreto de la pasarela

Lo cambia la cooperativa con Avalpay y lo envía desde **Destinos de pago › Credenciales**; el canal lo verifica contra la pasarela y
lo cifra. El ERP nunca lo guarda: sólo ve el estado de la verificación y los cuatro últimos caracteres del login.

## Revocar un dispositivo o cerrar sesiones

- El asociado: **Mi perfil › Dispositivos y sesiones** (revocar un dispositivo cierra sus sesiones y borra su credencial de confianza).
- La cooperativa: **Canal digital › Atención** busca a la persona y **Cerrar sesiones** (todas) o **Desbloquear** (tras intentos fallidos).
- Una llave de firma de la app comprometida: `docs/operaciones/canal-digital-app-tiendas.md` («Revocar»).
