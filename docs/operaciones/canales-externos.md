# Canales externos: proveedores, credenciales y efecto legal

> Feature 013 (cartera financiera), fase 2, T0100. Contrato:
> [contracts/adaptadores-y-archivos.md](../../specs/013-cartera-financiera/contracts/adaptadores-y-archivos.md) §2–§3 y §10,
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §21.2, [data-model.md](../../specs/013-cartera-financiera/data-model.md)
> parte 3 §4.7 (T71–T73).

## 1. Qué es un canal externo

Todo lo que el ERP le pide a un tercero fuera de su base pasa por un **puerto** de Application y un **adaptador** en el proyecto
`IngenIA365ERP.ExternalChannels`: mensajes a personas por SMS y WhatsApp, consulta de listas restrictivas, y —con sus entregas— depósito
de valores (pagarés desmaterializados) y centrales de riesgo. El correo no es un canal externo: sale por `IEmailSender` (`CORREO`).

Cada cooperativa elige, por **clase** y desde una fecha, qué canal atiende (`COR_ExternalChannelSettings`, una vigencia abierta por clase
y, en centrales, por central). La operación **sella** el código del canal con que se hizo; cambiar de canal sólo afecta lo nuevo.

| Canal | Qué es | Credencial | Validez legal |
|---|---|---|---|
| `SIMULADO` | adaptador de pruebas de cada puerto: responde según el último dígito del documento (mensajería 1/2/3/5; listas 8 posible coincidencia, 9 coincidencia) | no | **ninguna**: en producción su resultado no produce efecto |
| `CARGA-PROPIA` | listas restrictivas que mantiene el oficial de cumplimiento con la plantilla `compliance.restrictive-lists` (Cumplimiento › Listas restrictivas) | no | **sí**, también en producción |
| un proveedor real (`PROV-SMS`…) | el adaptador del proveedor contratado; hoy no hay ninguno instalado | sí, en el secreto | sí |

Un código sin adaptador en el despliegue no se puede configurar (`Core.ExternalChannel.ChannelUnknown`, con la lista de los
disponibles). Dos adaptadores del mismo puerto con el mismo código hacen fallar el arranque.

## 2. Configurar y verificar

Pantalla **Administración › Canales externos** (`/admin/canales-externos`; `Core.ExternalChannels.View` y `.Manage`):

1. «Nueva vigencia»: clase, central (sólo centrales de riesgo), canal, desde y motivo. La vigencia anterior de la misma clave se cierra
   la víspera; dos vigencias no se cruzan (`Core.ExternalChannel.Overlaps`).
2. Un proveedor real pide además la **clave de la credencial**, que tiene que ser exactamente la derivada de la cooperativa resuelta:
   `{tenantPublicId}.{channelCode}` (si no, `Core.ExternalChannel.CredentialMismatch` con la esperada). La auditoría no guarda la clave en
   el diff (`[NoAuditar]`).
3. «Verificar»: resuelve la credencial y llama `ProbarAsync` del adaptador; sella `VerifiedAt` sólo si respondió bien. Sin archivo
   responde `Core.ExternalChannel.CredentialMissing`.

Mientras una clase use `SIMULADO`, la pantalla muestra fijo **«Canal simulado: no tiene validez legal»**.

## 3. Credenciales: el Secret `erp-canales-externos`

Las credenciales **nunca** están en la base ni en `appsettings` ni en GitOps (`LasCredencialesDeCanalesExternosNoTocanLaBase`):

- Viven en el Secret de Kubernetes `erp-canales-externos` de cada ambiente, una clave por cooperativa y canal:
  `{tenantPublicId}.{channelCode}.json`. Qué campos lleva el JSON lo decide el adaptador.
- El Secret se monta como directorio (sin `subPath`) en `ExternalChannels:CredentialsPath` (`/secrets/canales-externos/`). El kubelet
  refresca el archivo solo; la API lo relee al vencer su caché de **5 minutos** por huella del archivo. No hace falta reiniciar.
- La ruta se deriva **sólo** del `PublicId` de la cooperativa resuelta y de un código de canal que cumple `^[A-Z0-9-]{2,30}$`: un código
  con `..` o `/` no lee nada.
- **El montaje en los manifiestos de GitOps es tarea aparte** (Polish de la 013) y, en producción, con autorización expresa.

### El guion

```powershell
.\tools\scripts\crear-secreto-canales-externos.ps1 -Ambiente qa -Tenant <guid> -Canal PROV-SMS -Campos usuario,clave,remitente,urlBase
```

- Pide los valores por teclado (ocultos) o los lee de STDIN (`-DesdeStdin`); viajan por SSH en STDIN, nunca como argumento, y no se
  escriben en disco.
- `kubectl patch secret erp-canales-externos` agrega o reemplaza **sólo** esa clave: las demás cooperativas y canales no se tocan.
- En producción exige escribir `PRODUCCION`.
- Rechaza `SIMULADO` y `CARGA-PROPIA` (no piden archivo) y valida el código con el mismo patrón que la API.
- Después, **verificar** el canal desde la pantalla.

## 4. El efecto legal en producción: una sola guardia

`GuardiaDeCanalesExternos` (`Application/Lending/Adapters`) es el único sitio que decide si un resultado externo produce efecto legal
(cuenta como aviso previo enviado, como consulta a centrales hecha, como pagaré depositado…). La configuración es
`CanalesExternosOptions.AmbienteDePruebas`, que la API fija con `!builder.Environment.IsProduction()` y nada más; **sin configurar vale
producción** (el defecto seguro).

- En pruebas (DEV, QA, e2e), un resultado del simulado tiene efecto, para poder recorrer los flujos completos.
- En producción, un resultado del simulado es **sin efecto**: el veredicto dice el motivo y qué configurar, y la operación que lo exige lo
  rechaza (`Lending.Adapter.SimulatedInProduction`). `SIMULADO` se puede dejar configurado en producción antes y después de activar
  Cartera: no hace daño, pero tampoco cumple la ley.
- `CARGA-PROPIA` no es simulado: vale en producción.

## 5. El circuito

`CircuitoDeCanalExterno` cuenta las fallas seguidas **por cooperativa y canal**. Al llegar al umbral (`Canales.UmbralDeFallas`, parámetro de
Cartera) el canal queda «suspendido por fallas»: no se le pide nada y se levanta `Cartera.CanalExternoNoDisponible` (una por clase y canal).
Pasados cinco minutos, la primera llamada sondea con `ProbarAsync`; si responde, el circuito se cierra y la alerta se da por atendida. El
circuito vive en memoria (cada réplica de la API tiene el suyo) y nunca lanza. SMS, WhatsApp y listas pasan por él; el correo no.

Las escrituras ante un tercero no se reintentan solas: un resultado `InProcess` (pudo llegar) se resuelve consultando, no reenviando.

## 6. Dónde está cada cosa

| | |
|---|---|
| Puertos y guardia | `Application/Common/ExternalChannels/`, `Application/Common/Messaging/`, `Application/Common/Compliance/IListasRestrictivas.cs`, `Application/Lending/Adapters/` |
| Adaptadores | `Infrastructure/IngenIA365ERP.ExternalChannels/` (`Credentials/`, `Circuit/`, `Messaging/MensajeriaSimulada.cs`, `RestrictiveLists/`); se componen con `AddExternalChannels` sólo en `API/Program.cs` |
| Configuración | `Application/Core/ExternalChannels/`; `API/Endpoints/Core/ExternalChannelsEndpoints.cs`; sección `ExternalChannels` de `API/appsettings.json` |
| Pantalla | `Shared/Pages/Administracion/CanalesExternos.razor` (`CanalesExternosClient`) |
| Guion | `tools/scripts/crear-secreto-canales-externos.ps1` |
| Pruebas | Application `GuardiaDeCanalesExternosTests`, `CredencialesDeCanalExternoEnArchivoTests`, baterías de conformidad; arquitectura `LosProveedoresExternosSoloLosConoceSuAdaptador`, `LasCredencialesDeCanalesExternosNoTocanLaBase`; e2e `PlataformaParaCarteraTests` |
