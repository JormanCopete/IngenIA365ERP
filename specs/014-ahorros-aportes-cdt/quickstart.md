# Quickstart: probar las captaciones de la Cartera (feature 014)

Guía para verificar cada historia de punta a punta en local y en las pruebas e2e. Los nombres de rutas,
permisos y tablas son los de [research.md](research.md) §0; los cuerpos, los de
[contracts/api.md](contracts/api.md).

## 0. Preparación

1. Docker Desktop corriendo (las e2e levantan PostgreSQL, SQL Server, Mongo y Redis con Testcontainers).
2. La 013 completa y verde en la rama base.
3. Compilar y correr las pruebas sin contenedores:

```bash
dotnet build IngenIA365ERP.slnx
```

```bash
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~Lending"
```

```bash
dotnet test tests/IngenIA365ERP.Architecture.Tests
```

4. Las e2e de captaciones:

```bash
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "Collection=Captaciones e2e"
```

En SQL Server, con `DB_PROVIDER=SqlServer`. La prueba de volumen sólo mide con `RUN_PERF_TESTS=1`.

5. Cooperativa de prueba: la fixture crea una cooperativa aislada, activa los créditos con una carga
   vacía, registra el **recaudador de prueba** y la **tesorería de prueba** como participantes (con
   `ProveedorDeCuentas` adoptado) y siembra los parámetros legales «pendiente de validar».

## 1. Casos dorados (SC-002, SC-019)

`Domain.Tests/Lending/Deposits/Casos/` debe pasar al centavo, como mínimo:

| Caso | Esperado |
|---|---|
| CDAT 10.000.000, 180 días, 10 % EA, al vencimiento, base 365 | 481.244 |
| Ídem base 360 | 488.088 |
| Ídem mensual vencido base 360 | 6 × 79.741 = 478.446 |
| Ídem mensual anticipado | 79.111 por mes |
| Ahorro 3 % EA, saldos 2 M / 5 M / 1,5 M por 10 días, saldo diario | 6.884, sin retención (umbral) |
| Ídem saldo mínimo | 3.649 |
| Contractual 12 × 200.000 al 6 % / al 8 % | 2.465.306 / 2.486.777 (bonificación 21.471) |
| GMF: retiros de 20.000.000 en el mes de la cuenta exenta (UVT 52.374) | 6.676 |
| GMF: redención CDAT abonada en cuenta (10.488.088) | 41.952 |

Y los 32 casos dorados de crédito siguen verdes después de agregar `CreditLine.Nature`.

## 2. Recorrido por historia

| Historia | Pasos | Comprobación |
|---|---|---|
| US1 Líneas | Crear línea de aportes y línea a la vista con tramos en `/cartera/lineas`; nueva versión con fecha futura | Consulta a fecha anterior devuelve la versión anterior; cooperativa sin autorización a captar rechaza la línea a la vista |
| US2 Apertura | Abrir cuenta conjunta con dos titulares y beneficiarios 60/40; marcar exenta de GMF; abrir otra exenta | La segunda marca se rechaza con `Lending.Gmf.AlreadyMarked`; dos aperturas simultáneas reciben números consecutivos |
| US2 Menor | Abrir cuenta de un menor con su representante; avanzar el reloj de prueba a sus 18 años | El representante deja de firmar, la cuenta queda «actualizar datos», llegan los dos avisos |
| US3 Aportes | Generar cuotas; pagar por el recaudador de prueba con `ConsignarAsync`; consultar `IConsultaDeAportes` v2 | Saldo de la consulta = extracto de aportes; la plantilla de saldos ya no se lee |
| US4 Movimientos | Consignar 1.000.000; retirar con un solo firmante en manejo conjunto; repetir la clave; retirar sobre la atribución | Rechazo por firmas; mismo comprobante sin duplicar; espera de aprobador con segundo factor |
| US5 Intereses | Saldos variables un mes; correr el ciclo diario; liquidar dos veces | Interés igual al caso dorado; la segunda liquidación devuelve la primera |
| US6 CDAT | Constituir 10.000.000 a 180 días con pago mensual; dejar vencer sin instrucción; pedir tasa negociada de 1,2 puntos con atribución de 0,5 | Seis pagos de 79.741 con su retención según el umbral; prórroga tras la gracia con tasa nueva; tasa negociada en espera del aprobador |
| US7 Contractual | 12 cuotas, pagar 10 | Incumplido con la tasa castigo |
| US8 Contabilidad | Con la matriz completa: consignar, causar, liquidar; quitar una regla | Comprobantes `MA`, `CC`, `TD` correctos; la pantalla bloquea con la regla faltante; conciliación en cero |
| US9 Migración | Cargar 500 cuentas con un error en la fila 37; corregir; cuadrar; activar captaciones | Error por fila y columna sin guardar nada; saldo inicial sin hechos; activación al día siguiente del corte |
| US10 Retiro | Radicar retiro; aprobar en sesión con acta; liquidar con un crédito vivo; reversar el pago del cruce | Mensaje a Asociados; cruce aplicado; devolución por orden de giro; liquidación «por rehacer» al reversar |
| US11 Embargos | Embargo sobre una persona con dos cuentas sobre el límite; embargo sobre aportes; embargo por alimentos | Sólo el exceso retenido; aportes rechazados con respuesta al juzgado; alimentos sin límite |
| US12 Asamblea | Revalorización al 5 % con IPC 4 % y fondo menor | Limitada al IPC y prorrateada al fondo |
| US13 Documentos | Extracto, certificado tributario anual; reversar una retención de diciembre | Certificado anulado y nuevo distinto |
| US14 Inactividad y fallecimiento | Dejar una cuenta sin movimientos; registrar fallecimiento con beneficiarios 50/50 | Inactiva (el abono de interés no la reactiva); dos órdenes de giro |
| US15 Liquidez | CDAT de varios vencimientos y permanente sólo retirable al retiro | Requerido por banda igual al cálculo manual |
| US16 Seguridad | Usuario de otra oficina consulta una cuenta; verificar la cadena de auditoría | Respuesta de inexistente; cadena verificada |
| US17 Informes | Generar SICSES de captaciones de un mes | Suma por línea igual a la conciliación; alerta «formato sin cotejar» |

## 3. Pagadurías (FR-125)

1. Crear un convenio con dos grupos de archivo (créditos; aportes y ahorros) y un concepto equivalente
   que agrupa la cuota de aportes y la de ahorro permanente con reparto por prelación.
2. Generar los archivos: deben salir dos, con sus formatos.
3. Cargar una respuesta que descuenta menos por el concepto agrupado: lo cubierto se aplica por
   prelación y el resto queda exigible como «no descontado por la pagaduría».
4. Cargar una respuesta con un empleado sin equivalencia: la fila queda en «no aplicados».

## 4. Retiro de lo heredado (R28)

1. Correr `diagnostico-captaciones-heredadas.sql` en local, DEV, QA y producción y guardar el resultado.
2. Si todo está en cero: respaldo por base, segundo revisor y la fila
   `COR_SystemSettings` `LND.CaptacionesHeredadas.RetiroAprobado`.
3. Aplicar la migración `RetiroDeCaptacionesHeredadas` con el DbMigrator. Sin la fila o con filas, se
   niega con el nombre de la tabla.
4. Verificar que `/api/savings/*`, `/api/cdt/*` y las pantallas heredadas responden 404 y que el menú no
   las enlaza (`TodoEnlaceDelMenuTieneSuPagina`).
