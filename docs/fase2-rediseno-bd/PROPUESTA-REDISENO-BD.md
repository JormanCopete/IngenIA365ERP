# PROPUESTA DE REDISEÑO DE BASE DE DATOS — IngenIA365ERP

> **Fecha:** 2026-03-20
> **Origen:** DBDefinicion.sql (SOLIDO ERP — SQL Server)
> **Estadísticas originales:** 270 tablas · 121 vistas · 8 funciones · 228 foreign keys

---

## REGLAS DEL NUEVO ESQUEMA

### Convención de nombres
| Elemento | Patrón | Ejemplo |
|---|---|---|
| Tablas | `[Prefijo3Letras]_[NombrePascalCaseInglésPluralizado]` | `LND_LoanPortfolios` |
| Columnas | PascalCase en inglés, descriptivas, sin abreviaciones | `IdentificationNumber` |
| Schema | `[dbo]` para definiciones, `tenant_NNN` para datos tenant | `tenant_001.LND_LoanPortfolios` |

### Prefijos por módulo
| Prefijo | Módulo | Tablas originales (prefijo) | Cantidad original |
|---|---|---|---|
| `COR_` | Core/Sistema | sys_*, cnt_nit | ~41 |
| `ACC_` | Accounting/Contabilidad | cnt_* | ~37 |
| `LND_` | Lending/Cartera Financiera | cop_*, dep_*, cre_* | ~107 |
| `PAY_` | Payroll/Nómina | nom_* | ~31 |
| `INV_` | Inventory/Inventario | inv_* | ~24 |
| `CDT_` | CDT/Certificados | cdt_* | ~7 |
| `TRS_` | Treasury/Tesorería | TES_* | ~3 |
| `DEB_` | Debit Cards | deb_* | ~7 |
| `SEC_` | Security/Seguridad | sys_sasusu, sys_menusu | ~5 |
| `AUD_` | Audit/Auditoría | *aud* | ~12 |
| `ADM_` | Admin/Multi-tenant | (nuevas) | 0 → nuevas |
| `WEB_` | Web/Online | web_* | ~6 |

### Estrategia de llaves primarias
- **Tablas MAESTRAS** (catálogos, personas, config): `INT IDENTITY(1,1)`
- **Tablas TRANSACCIONALES** (movimientos, liquidaciones, novedades): `BIGINT IDENTITY(1,1)`
- **TODA tabla** tiene además: `PublicId uniqueidentifier NOT NULL DEFAULT NEWID()` con UNIQUE INDEX
- **FKs** siempre referencian la PK (INT o BIGINT), NUNCA el PublicId
- La **API y frontend** solo ven PublicId (GUID), nunca el Id interno
- Las **PKs compuestas actuales** se convierten en UNIQUE INDEX

### Columnas estándar en TODA tabla
```sql
Id          INT|BIGINT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
PublicId    uniqueidentifier NOT NULL DEFAULT NEWID(),  -- UNIQUE INDEX
CreatedAt   datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
CreatedBy   nvarchar(100) NOT NULL,
UpdatedAt   datetime2 NULL,
UpdatedBy   nvarchar(100) NULL,
IsDeleted   bit NOT NULL DEFAULT 0,
DeletedAt   datetime2 NULL,
DeletedBy   nvarchar(100) NULL
```

---

## SECCIÓN 1: ANÁLISIS DEL ESQUEMA ACTUAL

### Resumen por módulo

#### Módulo sys_ (Sistema) — 41 tablas
| # | Tabla | Cols | PK | Tipo PK | FKs | Problemas |
|---|---|---|---|---|---|---|
| 1 | sys_maenit | ~130 | CODIGOTER varchar(14) | Simple varchar | Referenciada por ~50 tablas | Tabla monolítica: mezcla datos personales, laborales, financieros, cónyuge. PK varchar sin identity. ~130 columnas. smalldatetime. Campos FILLER |
| 2 | sys_agencia | 3 | codigo varchar(4) | Simple varchar | Referenciada por cop_*, nom_* | PK varchar, debería ser INT |
| 3 | sys_cencos | 10 | CCOSTO varchar(8) | Simple varchar | Referenciada por cop_*, nom_*, cnt_* | Centro de costo. PK varchar. Campos FILLER |
| 4 | sys_ciudad57 | 3 | CIUDAD int | Simple INT | Referenciada por sys_maenit | Ya tiene INT PK. Nombre críptico (57) |
| 5 | sys_compania | ~70 | (NIT) | Simple varchar | - | Tabla de empresa/compañía con ~70 columnas. Datos heterogéneos |
| 6 | sys_compro02 | ~35 | CODIGO varchar(4) | Simple varchar | Referenciada por cop_movimto, cnt_movimto | Comprobantes contables. PK varchar |
| 7 | sys_sasusu | ~20 | USUARIO varchar(14) | Simple varchar | - | Usuarios del sistema. PK varchar. Sin hash de password moderno |
| 8 | sys_banco03 | ~15 | codigo varchar(4) | Simple varchar | Referenciada por sys_maenit, cop_* | Bancos. PK varchar |
| 9 | sys_forpago | ~8 | Compuesta 2 cols | Compuesta | - | Formas de pago |
| 10 | sys_parent51 | 3 | codigo varchar(4) | Simple varchar | Referenciada por cop_benef | Parentescos |
| 11 | sys_periodo | ~10 | Compuesta 2 cols | Compuesta | - | Períodos contables |
| 12 | sys_seccion56 | ~3 | codigo varchar(4) | Simple varchar | - | Secciones |
| 13 | sys_profe52 | ~3 | codigo varchar(4) | Simple varchar | - | Profesiones |
| 14 | sys_cargo55 | ~3 | codigo varchar(4) | Simple varchar | - | Cargos |
| 15 | sys_cultura54 | ~3 | codigo varchar(4) | Simple varchar | - | Cultura |
| 16 | sys_deport53 | ~3 | codigo varchar(4) | Simple varchar | - | Deportes |
| 17 | sys_motret | ~3 | codigo varchar(4) | Simple varchar | - | Motivos de retiro |
| 18 | sys_entidad | ~5 | codigo varchar | Simple varchar | - | Entidades |
| 19 | sys_convenio | ~5 | codigo varchar | Simple varchar | - | Convenios |
| 20 | sys_curso | ~5 | codigo varchar | Simple varchar | - | Cursos |
| 21 | sys_paises | ~3 | codigo varchar | Simple varchar | - | Países |
| 22 | sys_referencia | ~8 | secuencia IDENTITY | Simple INT | - | Referencias. Ya tiene identity |
| 23 | sys_recreacion | ~12 | codigo varchar(15) | Simple varchar | - | Actividades recreación |
| 24 | sys_consecu | ~5 | - | - | - | Consecutivos |
| 25 | sys_parlistas | ~5 | - | - | - | Parámetros listas |
| 26 | sys_parfactura | ~10 | - | - | - | Parámetros facturación |
| 27 | sys_parpences | ~5 | - | - | - | Parámetros pensiones/cesantías |
| 28 | sys_menusu | 10 | SIN PK | - | - | **Sin PK**. Menú por usuario |
| 29 | sys_progra_sas | 3 | SIN PK | - | - | **Sin PK**. Programas SAS |
| 30 | sys_asejuri | ~5 | - | - | - | Asesores jurídicos |
| 31 | sys_enfermedades | ~3 | - | - | - | Catálogo enfermedades |
| 32 | sys_programa | ~5 | Compuesta 2 cols | Compuesta | - | Programas |
| 33 | sys_ComAsigna | ~5 | Compuesta 2 cols | Compuesta | - | Comités asignados |
| 34 | SYS_CODCIU | ~3 | - | - | - | Códigos ciudad (duplicado con sys_ciudad57) |
| 35-41 | sys_*aud (7 tablas) | ~varies | IDENTITY | Auditoría | - | Tablas de auditoría con IDENTITY |

#### Módulo cnt_ (Contabilidad) — 37 tablas
| # | Tabla | Cols | PK | Tipo PK | Problemas |
|---|---|---|---|---|---|
| 1 | cnt_maecuen | 81 | CUENTA varchar(12) | Simple varchar | Plan de cuentas. 81 columnas. Saldos mensuales desnormalizados (DEB_ENE..DEB_DIC, CRE_ENE..CRE_DIC) |
| 2 | cnt_nit | ~36 | NIT varchar(14) | Simple varchar | Terceros contables. Duplica datos de sys_maenit. PK varchar |
| 3 | cnt_movimto | ~25 | SECUENCIA IDENTITY | BIGINT | Movimientos contables. Ya tiene IDENTITY. Bien diseñada |
| 4 | cnt_salage | 32 | Compuesta 4 cols | Compuesta | Saldos por agencia. 24 columnas de saldos mensuales desnormalizados |
| 5 | cnt_docmto | ~15 | Compuesta 2 cols | Compuesta (COMPRONTE, NUMERO) | Documentos contables |
| 6 | cnt_docaux | ~15 | Compuesta 8 cols | Compuesta 8 cols! | Documentos auxiliares. PK de 8 columnas |
| 7 | cnt_tercero | ~8 | Compuesta 5 cols | Compuesta | Terceros por cuenta |
| 8 | cnt_amortiza | ~8 | Compuesta 6 cols | Compuesta | Amortizaciones |
| 9 | cnt_deprecia | ~10 | Compuesta múltiple | Compuesta | Depreciaciones |
| 10 | cnt_certrefte | 8 | SIN PK | - | **Sin PK**. Certificados rete-fuente |
| 11 | cnt_codforimpu | 5 | SIN PK | - | **Sin PK**. Formatos impuestos |
| 12 | cnt_estadosdecambio | 5 | SIN PK | - | **Sin PK**. Estados de cambio |
| 13 | cnt_grupocuenta | 7 | SIN PK | - | **Sin PK**. Grupos de cuenta |
| 14 | cnt_movitem | 13 | SIN PK | - | **Sin PK**. Items de movimiento |
| 15 | cnt_nombregrupo | 2 | SIN PK (tiene IDENTITY) | IDENTITY sin PK | Nombres de grupo |
| 16 | cnt_nombresubgrupo | 2 | SIN PK (tiene IDENTITY) | IDENTITY sin PK | Nombres subgrupo |
| 17 | cnt_saldocortolargo | 7 | SIN PK | - | **Sin PK**. Saldos corto/largo plazo |
| 18 | cnt_subgrupocuenta | 7 | SIN PK | - | **Sin PK**. Subgrupos de cuenta |
| 19-37 | cnt_concibanca, cnt_infmedian, cnt_lineagmf, cnt_lineaica, cnt_lineaiva, cnt_linearenta, cnt_linretefuente, cnt_maeconcibanca, cnt_parfordian, cnt_parinfmedian, cnt_parvalmedian, cnt_presupto, cnt_riesgo, cnt_cencos, cnt_estampilla, cnt_tmpflujocaja, cnt_maecuenAud, cnt_movaud | varies | Mixed | - | Parámetros y líneas contables |

#### Módulo cop_ (Cartera/Créditos) — 97 tablas
| # | Tabla | Cols | PK | Problemas |
|---|---|---|---|---|
| 1 | cop_maecar | ~120 | Compuesta 3 cols (CODIGOTER, LINCRED, NUMERO) | Maestro cartera. ~120 columnas. PK compuesta varchar+int+bigint. Campos FILLER. 50+ columnas de saldos |
| 2 | cop_movimto | ~45 | SECUENCIA IDENTITY | Movimientos cartera. Ya tiene IDENTITY |
| 3 | cop_cuopen | ~25 | Compuesta 5 cols | Cuotas pendientes. PK compuesta |
| 4 | cop_copclas | 21 | Compuesta 4 cols | Clasificación cartera |
| 5 | cop_concar12 | ~90 | LINCRED int | Parámetros líneas de crédito. ~90 columnas! |
| 6 | cop_docmto | ~18 | Compuesta 2 cols | Documentos |
| 7 | cop_nomdes | ~30 | Compuesta 10 cols! | Descuentos nómina. PK de 10 columnas |
| 8 | cop_valdesc | ~9 | Compuesta 8 cols | Valores descuento. PK de 8 columnas |
| 9 | cop_extras | ~15 | Compuesta 4 cols | Cuotas extras |
| 10 | cop_salmaecar | ~19 | Compuesta 4 cols | Saldos maestro cartera |
| 11 | cop_copmora | ~20 | Compuesta 5 cols | Mora |
| 12 | cop_caunov | ~10 | Compuesta 4 cols | Causación novedades |
| 13 | cop_liqmor | ~10 | Compuesta 5 cols | Liquidación mora |
| 14 | cop_benef | ~17 | Compuesta 2 cols | Beneficiarios |
| 15 | cop_empresa13 | ~22 | codigo_empresa varchar(4) | Empresas patronales |
| 16 | cop_comite | 4 | codigo varchar(4) | Comités |
| 17 | cop_asesores | ~8 | Idcedula varchar(14) | Asesores |
| 18 | cop_garantia | ~10 | Compuesta 3 cols | Garantías |
| 19 | cop_maeahor | ~45 | Compuesta múltiple | Maestro ahorros |
| 20 | cop_ahorro58 | ~15 | Compuesta | Parámetros ahorro |
| 21 | cop_codmov | ~11 | cod_movto varchar(2) | Códigos de movimiento |
| 22 | cop_solcre | ~80 | Compuesta | Solicitudes de crédito. ~80 columnas |
| 23 | cop_solaux | ~15 | IDENTITY | Solicitudes auxiliares. Ya tiene IDENTITY |
| 24 | cop_maegescob | ~10 | SECUENCIA IDENTITY | Gestión cobro. Ya tiene IDENTITY |
| 25 | cop_sipla_inusuales | ~15 | IDENTITY | SIPLA (lavado activos). Ya tiene IDENTITY |
| 26 | cop_actirecrea | ~10 | SECUENCIA IDENTITY | Actividades recreación. Ya tiene IDENTITY |
| 27-97 | Restantes cop_* | varies | Mixed | Parámetros, auditoría, relaciones secundarias |

#### Módulo nom_ (Nómina) — 31 tablas
| # | Tabla | Cols | PK | Problemas |
|---|---|---|---|---|
| 1 | nom_empleados | ~80 | Compuesta 2 cols (idnomina, idempleado) | Empleados. Debería separar datos personales |
| 2 | nom_liqplan | 14 | Compuesta 5 cols | Liquidación planilla. PK de 5 columnas |
| 3 | nom_movtos | ~15 | Compuesta 5 cols | Movimientos nómina |
| 4 | nom_novedad | ~15 | Compuesta 3 cols | Novedades |
| 5 | nom_cptos | ~40 | Simple | Conceptos nómina |
| 6 | nom_empresas | ~30 | Simple | Empresas nómina |
| 7 | nom_cencos | ~5 | Simple | Centros de costo nómina |
| 8 | nom_perpagos | ~10 | Compuesta 2 cols | Períodos de pago |
| 9 | nom_parautapo | 44 | SIN PK | **Sin PK**. 44 columnas sin llave |
| 10 | nom_parent | 3 | SIN PK | **Sin PK**. Parentescos nómina |
| 11 | nom_pensiones | 5 | SIN PK | **Sin PK**. Pensiones |
| 12-31 | Restantes nom_* | varies | Mixed | Auditoría, parámetros, certificados |

#### Módulo inv_ (Inventario) — 24 tablas
| # | Tabla | Cols | PK | Problemas |
|---|---|---|---|---|
| 1 | inv_productos | ~30 | IdProducto INT | Productos. Ya tiene INT PK |
| 2 | inv_movtos | 29 | SIN PK | **Sin PK**. 29 columnas. Usa float para Secuencia |
| 3 | inv_tipomovtos | ~21 | IdTipoMovto smallint | Tipos de movimiento |
| 4 | inv_facturas | 35 | SIN PK | **Sin PK**. 35 columnas sin llave |
| 5 | inv_dstos | 16 | SIN PK | **Sin PK**. Descuentos sin llave |
| 6 | inv_docs | ~10 | Compuesta 2 cols | Documentos |
| 7 | inv_grupos | ~5 | IdGruProducto INT | Grupos. Ya tiene INT PK |
| 8 | inv_precios | ~8 | Compuesta 3 cols | Precios |
| 9-24 | Restantes inv_* | varies | Mixed | Bodegas, puntos, turnos, comisiones |

#### Módulo dep_ (Depósitos/Ahorros) — 9 tablas
| # | Tabla | Cols | PK | Problemas |
|---|---|---|---|---|
| 1 | dep_maeahor | ~50 | num_cuenta varchar | Maestro ahorros. PK varchar |
| 2 | dep_novmeahor | ~15 | Compuesta 4 cols | Novedades ahorro |
| 3 | dep_firmas | ~8 | Compuesta 2 cols | Firmas |
| 4 | dep_sellos | ~5 | num_cuenta | Sellos |
| 5 | dep_cajeros | ~8 | Compuesta 3 cols | Cajeros |
| 6-9 | dep_basecaja, dep_checanje, dep_talonario, dep_maeaud | varies | Mixed | Canje, talonarios, auditoría |

#### Módulo cdt_ (CDTs) — 7 tablas
| # | Tabla | Cols | PK | Problemas |
|---|---|---|---|---|
| 1 | cdt_maecdats | ~40 | Compuesta | Maestro CDTs |
| 2 | cdt_novcdats | ~15 | Compuesta 4 cols | Novedades CDTs |
| 3 | cdt_parame58 | ~30 | Simple | Parámetros CDT |
| 4 | cdt_tasasplazos | ~5 | Compuesta 5 cols | Tasas por plazo |
| 5 | cdt_asoreferencia | 9 | SIN PK | **Sin PK** |
| 6-7 | cdt_maeaud, cdt_paramaud | varies | IDENTITY | Auditoría |

#### Módulo deb_ (Tarjeta Débito) — 7 tablas
| Tabla | PK | Problemas |
|---|---|---|
| deb_maetarj | Compuesta (Banco, Tarjeta) | Maestro tarjetas |
| deb_movto | Compuesta (Secuencia, Tarjeta) | Movimientos |
| deb_pardatafonos | SIN PK | **Sin PK** |
| deb_enpacto, deb_enpactors, deb_parconv, deb_pardiario | Mixed | Parámetros |

#### Módulo TES_ (Tesorería) — 3 tablas
| Tabla | PK | Problemas |
|---|---|---|
| TES_CHEQUES | Compuesta 3 cols | Cheques |
| TES_CPTOS | Simple | Conceptos |
| TES_FACTURA | Compuesta 3 cols | Facturas tesorería |

#### Módulo web_ (Web) — 6 tablas
| Tabla | PK | Problemas |
|---|---|---|
| web_solcred, web_solaux, web_solafi, web_maeser, web_extras, web_actdatos | Compuestas | Solicitudes web |

#### Tablas sueltas
| Tabla | Problemas |
|---|---|
| cre_parame01 | Parámetros créditos. Compuesta |
| lin_consulta | Consultas línea. IDENTITY |
| logo | Logo. 1 columna + id |
| plano | SIN PK. 1 columna |
| wrk_tem1 | SIN PK. Temporal |
| TBLZONAZ | SIN PK. Zonas |

### Problemas globales detectados

| Problema | Cantidad | Impacto |
|---|---|---|
| Tablas SIN PK | 34 | Integridad comprometida |
| PKs compuestas (2+ columnas) | 122 | JOINs complejos, no hay surrogate key |
| PKs compuestas de 5+ columnas | 28 | Extremadamente difícil de mantener |
| PKs varchar (no IDENTITY) | ~80 | Sin auto-incremento, dependencia de negocio |
| `varchar` en vez de `nvarchar` | ~95% tablas | No soporta Unicode/caracteres especiales |
| `smalldatetime` | ~100+ columnas | Rango limitado (1900-2079), precisión 1 minuto |
| Columnas FILLER* | ~20 tablas | Espacio desperdiciado, diseño legacy |
| Saldos mensuales desnormalizados | cnt_maecuen, cnt_salage | 24+ columnas redundantes (DEB_ENE...DEB_DIC) |
| Tabla sys_maenit monolítica | 1 | ~130 columnas mezclando persona, asociado, empleado, cónyuge |
| Tablas con 80+ columnas | 6 | cop_maecar, sys_maenit, cop_concar12, cop_solcre, nom_empleados, cnt_maecuen |
| Tablas duplicadas (cnt_nit ↔ sys_maenit) | 2 | Datos de persona en 2 tablas distintas |

---

## SECCIÓN 2: TABLAS A ELIMINAR

| # | Tabla | Razón de eliminación | Evidencia |
|---|---|---|---|
| 1 | `wrk_tem1` | Tabla temporal de trabajo | Prefijo wrk_, 4 columnas, sin PK, sin relaciones |
| 2 | `plano` | Tabla temporal para generación de planos bancarios | 1 columna, sin PK, se recrea en cada proceso |
| 3 | `logo` | Almacena imagen de logo en BD | 1 fila, debe ir como recurso estático en storage |
| 4 | `TBLZONAZ` | Tabla huérfana de zonas | 3 columnas, sin PK, sin FKs, duplica cop_zonas/cop_subzonas |
| 5 | `cnt_tmpflujocaja` | Tabla temporal para cálculo flujo de caja | Prefijo tmp implica temporal |
| 6 | `cop_tempmovextra` | Temporal para movimientos extra | 12 columnas, sin PK, sin relaciones |
| 7 | `SYS_CODCIU` | Duplicado de sys_ciudad57 | Misma función: códigos de ciudad |
| 8 | `cnt_saldocortolargo` | Temporal para clasificación saldos | 7 columnas, sin PK, calculable desde cnt_salage |
| 9 | `cop_tmplavado` | Temporal para proceso lavado activos | Datos transitorios, regenerable |
| 10 | `cnt_certrefte` | Sin PK, datos calculables | 8 columnas, certificados rete-fuente regenerables |
| 11 | `sys_progra_sas` | Obsoleta - programas del sistema legacy SAS | 3 columnas, sin PK, no referenciada |
| 12 | `cop_retiro` | Duplicada con cop_retiros | 5 columnas, sin PK, cop_retiros es la activa |

**Total eliminadas: 12 tablas**

---

## SECCIÓN 3: TABLAS A FUSIONAR

### 3.1 CENTRALIZACIÓN DE PERSONAS: COR_People

**Tablas fusionadas:** `sys_maenit` + `cnt_nit`

La tabla maestra `COR_People` centraliza TODOS los datos de cualquier persona o entidad del sistema.

| Dato | Origen sys_maenit | Origen cnt_nit | Destino |
|---|---|---|---|
| Identificación | CODIGOTER, NIT | NIT | COR_People.IdentificationNumber |
| Tipo documento | TIPO_NIT | TIPO_NIT | COR_People.IdentificationType |
| Dígito verificación | NIT_CHEQUEO | NIT_CHEQUEO | COR_People.CheckDigit |
| Nombre | NOMBRE | NOMBRE | COR_People.FirstName |
| Apellido | APELLIDO | RAZON_SOCIAL | COR_People.LastName |
| Dirección | DIRECCION | DIRECCION | COR_People.Address |
| Teléfono | TELEFONO1 | TELEFONO1 | COR_People.Phone |
| Móvil | MOVIL | - | COR_People.Mobile |
| Email | EMAIL | email | COR_People.Email |
| Ciudad | DPTO_CIUDAD | IdCiudad | COR_People.CityId (FK → COR_Cities) |
| Sexo | SEXO | - | COR_People.Gender |
| Estado civil | ESTADO_CIVIL | - | COR_People.MaritalStatus |
| Fecha nacimiento | FECNACEM | - | COR_People.DateOfBirth |
| Tipo persona | - | tipo_persona | COR_People.PersonType |
| Es natural | NATJUR | - | COR_People.IsNaturalPerson |

**Campos que van a COR_Associates (datos de asociado):**
- FECHA_INGRESO → JoinDate
- SALDO_APORTE → (calculado desde LND)
- TASA_APORTE → ContributionRate
- SECCION_EMPRESA → SectionCode
- EMPRESA → CompanyCode
- AGENCIA → BranchCode
- DIA_CORTE → CutoffDay
- ESTADO → Status
- CALIFI_CATEG → CategoryRating
- CLAVE_INTERNET → (migra a SEC_Users)
- ASESOR → AdvisorId
- SECCIO → SectionId
- COBROJUR → LegalCollection
- CONTRACTO → ContractNumber
- Datos cónyuge → COR_Spouses (tabla separada)
- Datos categorías → COR_AssociateCategories

**Campos que van a PAY_Employees (datos laborales):**
- SALARIO → Salary
- TIPO_SALARIO → SalaryType
- EMPRESA_LABORA → EmployerName
- FEING_EMPRESA → EmployerJoinDate
- CARGO → PositionCode
- PROFESION → ProfessionCode
- CESANTIAS → SeverancePay
- OTRO_INGRESO → OtherIncome
- Empleado → (flag IsEmployee en COR_People)

**Campos que van a COR_PeopleFinancial (datos financieros):**
- CAPA_DEUDA → DebtCapacity
- ACTIVOS → TotalAssets
- IngrVariables → VariableIncome
- IngArriendos → RentalIncome
- DeudasTerceros → ThirdPartyDebts
- GASTO_FIJO_MES → MonthlyFixedExpenses
- ScoreCifin → CreditScore
- califidatacredito → CreditBureauRating

**Deduplicación:** Se usa NIT (cnt_nit) = CODIGOTER (sys_maenit) como llave de cruce. Cuando existan en ambas tablas, sys_maenit prevalece para datos personales y cnt_nit para datos contables (régimen, tipo ICA, gran contribuyente).

### 3.2 Fusión cnt_maecuen + cnt_salage

**Problema actual:** cnt_maecuen tiene 24 columnas DEB_ENE...DEB_DIC y 24 columnas CRE_ENE...CRE_DIC desnormalizadas. cnt_salage repite el patrón con saldos por agencia.

**Solución:**
- `ACC_ChartOfAccounts` → Solo metadatos de la cuenta (nombre, naturaleza, nivel, flags)
- `ACC_AccountBalances` → Tabla normalizada: (AccountId, PeriodId, BranchId, CostCenterId, DebitAmount, CreditAmount)

### 3.3 Fusión cop_empresa13 + nom_empresas

Ambas almacenan empresas patronales. Se unifican en `COR_EmployerCompanies`.

### 3.4 Fusión sys_cencos + nom_cencos + cnt_cencos

Tres tablas de centros de costo para diferentes módulos. Se unifican en `COR_CostCenters`.

### 3.5 Fusión cop_benef + nom_bene

Beneficiarios de asociados (cop_benef) y beneficiarios de empleados (nom_bene). Se unifican en `COR_Beneficiaries` con campo `BeneficiaryType`.

### 3.6 Fusión sys_parent51 + nom_parent

Ambas son catálogos de parentescos. Se unifican en `COR_Relationships`.

### 3.7 Fusión cop_actiaso + cop_actirecrea + cop_novactividad

Actividades de asociados. Se unifican en `LND_AssociateActivities` con `LND_ActivityEnrollments`.

### 3.8 Fusión cop_seguros + cop_seguross + cop_benefseg

Tres tablas de seguros. Se unifican en `LND_InsurancePolicies` + `LND_InsuranceBeneficiaries`.

### 3.9 Fusión cop_redapo + cop_redaportes + cop_parredaportes

Tres tablas de aportes. Se unifican en `LND_ContributionReductions` + `LND_ContributionReductionParams`.

**Total fusiones: 9 grupos → reducen ~25 tablas a ~12**

---

## SECCIÓN 4: TABLAS NUEVAS A CREAR

### Multi-tenant y administración

| # | Tabla Nueva | Descripción | PK |
|---|---|---|---|
| 1 | `ADM_Tenants` | Gestión de tenants: id, name, schema, plan, isActive, createdAt | INT |
| 2 | `ADM_Subscriptions` | Planes y billing por tenant: tenantId, plan, startDate, endDate, status | INT |
| 3 | `ADM_TenantSettings` | Configuración específica por tenant (key-value) | INT |

### Seguridad moderna

| # | Tabla Nueva | Descripción | PK |
|---|---|---|---|
| 4 | `SEC_Users` | Reemplaza sys_sasusu: email, passwordHash, isActive, emailVerified, mfaEnabled | INT |
| 5 | `SEC_Roles` | Roles del sistema: name, description, isSystemRole | INT |
| 6 | `SEC_Permissions` | Permisos granulares: resource, action, description | INT |
| 7 | `SEC_RolePermissions` | Relación roles-permisos (N:M) | INT |
| 8 | `SEC_UserRoles` | Relación usuarios-roles (N:M) | INT |
| 9 | `SEC_RefreshTokens` | JWT refresh token rotation: userId, token, expiresAt, revokedAt | BIGINT |
| 10 | `SEC_UserSessions` | Control de sesiones activas: userId, ipAddress, userAgent, lastActivity | BIGINT |
| 11 | `SEC_LoginAttempts` | Registro de intentos de login: email, ipAddress, success, attemptedAt | BIGINT |

### Core/Sistema

| # | Tabla Nueva | Descripción | PK |
|---|---|---|---|
| 12 | `COR_SystemSettings` | Configuración por tenant, key-value tipado | INT |
| 13 | `COR_NotificationTemplates` | Plantillas de email/SMS/push | INT |
| 14 | `COR_Notifications` | Notificaciones enviadas | BIGINT |
| 15 | `COR_Attachments` | Archivos adjuntos (referencia a blob storage) | BIGINT |
| 16 | `COR_Spouses` | Datos del cónyuge (extraídos de sys_maenit) | INT |
| 17 | `COR_PeopleFinancial` | Datos financieros de persona (extraídos de sys_maenit) | INT |
| 18 | `COR_AssociateCategories` | Categorías de asociado (extraídas de sys_maenit) | INT |
| 19 | `COR_Countries` | Países (normalizado desde sys_paises) | INT |
| 20 | `COR_Departments` | Departamentos/estados | INT |

### Auditoría

| # | Tabla Nueva | Descripción | PK |
|---|---|---|---|
| 21 | `AUD_AuditReferences` | Referencia a logs detallados en MongoDB: entityType, entityId, action, userId, timestamp, mongoDocId | BIGINT |

### Contabilidad

| # | Tabla Nueva | Descripción | PK |
|---|---|---|---|
| 22 | `ACC_AccountBalances` | Saldos normalizados por cuenta/período/agencia/ccosto | BIGINT |
| 23 | `ACC_FiscalPeriods` | Períodos fiscales con estado (abierto/cerrado) | INT |

**Total tablas nuevas: 23**

---

## SECCIÓN 5: PROPUESTA DE NOMBRES NUEVOS

### Módulo COR_ (Core/Sistema)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 1 | sys_maenit + cnt_nit | COR_People | INT | Tabla maestra centralizada de personas/entidades |
| 2 | (nueva) | COR_Associates | INT | Datos específicos de asociado |
| 3 | (nueva) | COR_Spouses | INT | Datos del cónyuge (extraídos de sys_maenit) |
| 4 | (nueva) | COR_PeopleFinancial | INT | Datos financieros de persona |
| 5 | (nueva) | COR_AssociateCategories | INT | Categorías del asociado |
| 6 | sys_agencia | COR_Branches | INT | Agencias/sucursales |
| 7 | sys_cencos + nom_cencos + cnt_cencos | COR_CostCenters | INT | Centros de costo unificados |
| 8 | sys_ciudad57 | COR_Cities | INT | Ciudades |
| 9 | sys_paises | COR_Countries | INT | Países |
| 10 | (nueva) | COR_Departments | INT | Departamentos/estados |
| 11 | sys_compania | COR_Companies | INT | Datos de la compañía |
| 12 | sys_banco03 | COR_Banks | INT | Catálogo de bancos |
| 13 | sys_seccion56 | COR_Sections | INT | Secciones organizacionales |
| 14 | sys_profe52 | COR_Professions | INT | Catálogo de profesiones |
| 15 | sys_cargo55 | COR_Positions | INT | Catálogo de cargos |
| 16 | sys_cultura54 | COR_CulturalActivities | INT | Actividades culturales |
| 17 | sys_deport53 | COR_Sports | INT | Deportes |
| 18 | sys_motret | COR_WithdrawalReasons | INT | Motivos de retiro |
| 19 | sys_entidad | COR_Entities | INT | Entidades externas |
| 20 | sys_convenio | COR_Agreements | INT | Convenios |
| 21 | sys_curso | COR_Courses | INT | Cursos |
| 22 | sys_parent51 + nom_parent | COR_Relationships | INT | Parentescos |
| 23 | sys_referencia | COR_References | INT | Referencias personales |
| 24 | sys_enfermedades | COR_Diseases | INT | Catálogo de enfermedades |
| 25 | cop_comite | COR_Committees | INT | Comités |
| 26 | cop_comiteasoc | COR_CommitteeMembers | INT | Miembros de comité |
| 27 | cop_benef + nom_bene | COR_Beneficiaries | INT | Beneficiarios unificados |
| 28 | sys_recreacion | COR_RecreationalEvents | INT | Eventos recreativos |
| 29 | sys_forpago | COR_PaymentMethods | INT | Formas de pago |
| 30 | sys_forpago_cheq | COR_PaymentMethodChecks | INT | Cheques por forma de pago |
| 31 | cop_empresa13 + nom_empresas | COR_EmployerCompanies | INT | Empresas patronales |
| 32 | cop_asesores | COR_Advisors | INT | Asesores |
| 33 | cop_progactividad | COR_ActivityPrograms | INT | Programas de actividades |
| 34 | sys_consecu | COR_Sequences | INT | Consecutivos del sistema |
| 35 | sys_parlistas | COR_ListParameters | INT | Parámetros de listas |
| 36 | sys_parfactura | COR_InvoiceParameters | INT | Parámetros de facturación |
| 37 | sys_parpences | COR_PensionSeveranceParams | INT | Parámetros pensiones/cesantías |
| 38 | sys_asejuri | COR_LegalAdvisors | INT | Asesores jurídicos |
| 39 | (nueva) | COR_SystemSettings | INT | Configuración por tenant |
| 40 | (nueva) | COR_NotificationTemplates | INT | Plantillas notificación |
| 41 | (nueva) | COR_Notifications | BIGINT | Notificaciones enviadas |
| 42 | (nueva) | COR_Attachments | BIGINT | Archivos adjuntos |

### Módulo ACC_ (Contabilidad)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 43 | cnt_maecuen | ACC_ChartOfAccounts | INT | Plan de cuentas (solo metadatos) |
| 44 | cnt_salage | ACC_AccountBalances | BIGINT | Saldos normalizados por cuenta/período/agencia/ccosto |
| 45 | cnt_movimto | ACC_JournalEntries | BIGINT | Asientos contables (ya tenía IDENTITY) |
| 46 | cnt_docmto | ACC_Documents | BIGINT | Documentos contables |
| 47 | cnt_docaux | ACC_AuxiliaryDocuments | BIGINT | Documentos auxiliares |
| 48 | cnt_tercero | ACC_ThirdPartyAccounts | BIGINT | Terceros por cuenta |
| 49 | cnt_amortiza | ACC_Amortizations | BIGINT | Amortizaciones |
| 50 | cnt_deprecia | ACC_Depreciations | BIGINT | Depreciaciones |
| 51 | cnt_movitem | ACC_JournalEntryItems | BIGINT | Ítems de asiento |
| 52 | cnt_presupto | ACC_Budgets | INT | Presupuestos |
| 53 | cnt_riesgo | ACC_RiskCategories | INT | Categorías de riesgo |
| 54 | cnt_concibanca | ACC_BankReconciliations | BIGINT | Conciliaciones bancarias |
| 55 | cnt_concibancaplano | ACC_BankReconciliationFlats | BIGINT | Planos conciliación |
| 56 | cnt_maeconcibanca | ACC_BankReconciliationMasters | INT | Maestro conciliaciones |
| 57 | cnt_lineagmf | ACC_GmfTaxLines | INT | Líneas GMF |
| 58 | cnt_lineaica | ACC_IcaTaxLines | INT | Líneas ICA |
| 59 | cnt_lineaiva | ACC_VatTaxLines | INT | Líneas IVA |
| 60 | cnt_linearenta | ACC_IncomeTaxLines | INT | Líneas renta |
| 61 | cnt_linretefuente | ACC_WithholdingTaxLines | INT | Líneas rete-fuente |
| 62 | cnt_parfordian | ACC_DianReportFormats | INT | Formatos DIAN |
| 63 | cnt_parinfmedian | ACC_FinancialReportParams | INT | Parámetros informes |
| 64 | cnt_parvalmedian | ACC_FinancialReportValues | INT | Valores informes |
| 65 | cnt_infmedian | ACC_FinancialReports | INT | Informes financieros |
| 66 | cnt_codforimpu | ACC_TaxFormCodes | INT | Códigos formatos impuestos |
| 67 | cnt_estampilla | ACC_StampTaxes | INT | Estampillas |
| 68 | cnt_grupocuenta | ACC_AccountGroups | INT | Grupos de cuenta |
| 69 | cnt_subgrupocuenta | ACC_AccountSubgroups | INT | Subgrupos de cuenta |
| 70 | cnt_nombregrupo | ACC_GroupNames | INT | Nombres de grupo |
| 71 | cnt_nombresubgrupo | ACC_SubgroupNames | INT | Nombres subgrupo |
| 72 | cnt_estadosdecambio | ACC_ExchangeRateHistory | BIGINT | Histórico tasas de cambio |
| 73 | (nueva) | ACC_FiscalPeriods | INT | Períodos fiscales |
| 74 | sys_compro02 | ACC_VoucherTypes | INT | Tipos de comprobante |
| 75 | sys_periodo | ACC_AccountingPeriods | INT | Períodos contables |

### Módulo LND_ (Lending/Cartera Financiera)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 76 | cop_maecar | LND_LoanPortfolios | INT | Maestro de créditos/cartera |
| 77 | cop_movimto | LND_Transactions | BIGINT | Movimientos de cartera |
| 78 | cop_cuopen | LND_PendingInstallments | BIGINT | Cuotas pendientes |
| 79 | cop_copclas | LND_PortfolioClassifications | BIGINT | Clasificación de cartera |
| 80 | cop_concar12 | LND_CreditLineParameters | INT | Parámetros líneas de crédito |
| 81 | cop_docmto | LND_Documents | BIGINT | Documentos cartera |
| 82 | cop_extras | LND_ExtraPayments | BIGINT | Cuotas extras |
| 83 | cop_salmaecar | LND_PortfolioBalances | BIGINT | Saldos de cartera por período |
| 84 | cop_copmora | LND_DefaultRecords | BIGINT | Registros de mora |
| 85 | cop_caunov | LND_AccrualEntries | BIGINT | Causaciones |
| 86 | cop_liqmor | LND_DefaultLiquidations | BIGINT | Liquidación de mora |
| 87 | cop_codmov | LND_TransactionCodes | INT | Códigos de movimiento |
| 88 | cop_garantia | LND_Guarantees | INT | Garantías |
| 89 | cop_solcre | LND_LoanApplications | BIGINT | Solicitudes de crédito |
| 90 | cop_solaux | LND_AuxiliaryApplications | BIGINT | Solicitudes auxiliares |
| 91 | cop_solauxcuota | LND_AuxAppInstallments | BIGINT | Cuotas solicitudes auxiliares |
| 92 | cop_solauxcuotabenef | LND_AuxAppInstallmentBeneficiaries | BIGINT | Beneficiarios cuotas aux |
| 93 | cop_solbienes | LND_ApplicationAssets | BIGINT | Bienes en solicitud |
| 94 | cop_solcodeudor | LND_ApplicationCodebtors | BIGINT | Codeudores en solicitud |
| 95 | cop_solreferencia | LND_ApplicationReferences | BIGINT | Referencias en solicitud |
| 96 | cop_solparviv | LND_HousingApplicationParams | INT | Parámetros solicitud vivienda |
| 97 | cop_extrasoli | LND_ApplicationExtras | BIGINT | Extras en solicitud |
| 98 | cop_estsol | LND_ApplicationStatuses | INT | Estados de solicitud |
| 99 | cop_linsolaux | LND_AuxiliaryApplicationLines | BIGINT | Líneas solicitud auxiliar |
| 100 | cop_nomdes | LND_PayrollDeductions | BIGINT | Descuentos nómina cartera |
| 101 | cop_valdesc | LND_DeductionValues | BIGINT | Valores de descuento |
| 102 | cop_nomconce | LND_PayrollDeductionConcepts | INT | Conceptos descuento nómina |
| 103 | cop_nompla | LND_PayrollDeductionPeriods | INT | Períodos descuento nómina |
| 104 | cop_nomnov | LND_PayrollDeductionEntries | BIGINT | Novedades descuento nómina |
| 105 | cop_maeahor | LND_SavingsAccounts | INT | Maestro cuentas de ahorro |
| 106 | cop_ahorro58 | LND_SavingsParameters | INT | Parámetros de ahorro |
| 107 | cop_claint | LND_InterestRates | INT | Tasas de interés |
| 108 | cop_maegescob | LND_CollectionCases | BIGINT | Gestión de cobro |
| 109 | cop_gesmaes | LND_CollectionMasters | INT | Maestro gestión cobro |
| 110 | cop_gespara | LND_CollectionPeriods | INT | Períodos gestión cobro |
| 111 | cop_novfecgestion | LND_CollectionDateEntries | BIGINT | Novedades fecha gestión |
| 112 | cop_maecircobro | LND_CollectionNotices | BIGINT | Circulares de cobro |
| 113 | cop_detcircobro | LND_CollectionNoticeDetails | BIGINT | Detalle circulares cobro |
| 114 | cop_paracircular | LND_CollectionNoticeParams | INT | Parámetros circulares |
| 115 | cop_riesgo | LND_RiskAssessments | BIGINT | Evaluaciones de riesgo |
| 116 | cop_cuoant | LND_PreviousInstallments | BIGINT | Cuotas anteriores |
| 117 | cop_parprov | LND_ProvisionParameters | INT | Parámetros provisiones |
| 118 | cop_retiros | LND_AssociateWithdrawals | BIGINT | Retiros de asociados |
| 119 | cop_acta | LND_Minutes | INT | Actas |
| 120 | cop_asoacta | LND_MinuteAttendees | BIGINT | Asistentes a actas |
| 121 | cop_maerest | LND_LoanRestructurings | BIGINT | Reestructuraciones |
| 122 | cop_carteraclp | LND_ShortLongTermPortfolio | BIGINT | Cartera corto/largo plazo |
| 123 | cop_percau | LND_AccrualPeriods | BIGINT | Períodos de causación |
| 124 | cop_seguros + cop_seguross + cop_benefseg | LND_InsurancePolicies | INT | Pólizas de seguro unificadas |
| 125 | (nueva) | LND_InsuranceBeneficiaries | INT | Beneficiarios de seguros |
| 126 | cop_redapo + cop_redaportes + cop_parredaportes | LND_ContributionReductions | BIGINT | Reducción de aportes unificada |
| 127 | (nueva) | LND_ContributionReductionParams | INT | Parámetros reducción aportes |
| 128 | cop_actiaso + cop_actirecrea + cop_novactividad | LND_AssociateActivities | INT | Actividades de asociado |
| 129 | (nueva) | LND_ActivityEnrollments | BIGINT | Inscripciones a actividades |
| 130 | cop_maenitbienes | LND_PersonAssets | INT | Bienes de personas |
| 131 | cop_huellafirma | LND_BiometricRecords | INT | Huellas y firmas |
| 132 | cop_listanegra | LND_Blacklist | INT | Lista negra |
| 133 | cop_declavado | LND_MoneyLaunderingDeclarations | BIGINT | Declaraciones lavado activos |
| 134 | cop_salextras | LND_ExtraPaymentBalances | BIGINT | Saldos extras |
| 135 | cop_detallefactura | LND_InvoiceDetails | INT | Detalles factura |
| 136 | cop_facturacartera | LND_PortfolioInvoices | BIGINT | Facturas cartera |
| 137 | cop_maefact | LND_InvoiceMasters | INT | Maestro facturas |
| 138 | cop_detfact | LND_InvoiceLineItems | BIGINT | Líneas de factura |
| 139 | cop_enfermedadAsoc | LND_AssociateDiseases | INT | Enfermedades de asociado |
| 140 | cop_estretiro | LND_WithdrawalStatuses | INT | Estados de retiro |
| 141 | cop_parviv | LND_HousingParameters | INT | Parámetros vivienda |
| 142 | cop_param_sipla | LND_SiplaParameters | INT | Parámetros SIPLA |
| 143 | cop_param_grup_sipla | LND_SiplaGroupParameters | INT | Parámetros grupo SIPLA |
| 144 | cop_sipla_inusuales | LND_UnusualTransactions | BIGINT | Transacciones inusuales |
| 145 | cop_Sipla_Novedades | LND_UnusualTransactionEntries | BIGINT | Novedades SIPLA |
| 146 | cop_paramscoring | LND_ScoringParameters | INT | Parámetros scoring |
| 147 | cop_rangoscoring | LND_ScoringRanges | INT | Rangos scoring |
| 148 | cop_paramPeriocidad | LND_PeriodicityParameters | INT | Parámetros periodicidad |
| 149 | cop_partasasplazo | LND_RatesByTerm | INT | Tasas por plazo |
| 150 | cop_tasasplazos | LND_TermRates | INT | Plazos y tasas |
| 151 | cop_copctas | LND_PortfolioAccounts | INT | Cuentas de cartera |
| 152 | cop_zonas | LND_Zones | INT | Zonas |
| 153 | cop_subzonas | LND_SubZones | INT | Sub-zonas |
| 154 | cop_Tipozonas | LND_ZoneTypes | INT | Tipos de zona |
| 155 | cop_auxilio | LND_Subsidies | INT | Auxilios |
| 156 | COP_SOLRECR | LND_RecreationApplications | INT | Solicitudes recreación |
| 157 | cop_LinAud | LND_CreditLineAudit | BIGINT | Auditoría líneas crédito |
| 158 | cre_parame01 | LND_CreditParameters | INT | Parámetros crédito |

### Módulo LND_ — Depósitos/Ahorros (antes dep_)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 159 | dep_maeahor | LND_DepositAccounts | INT | Cuentas de depósito/ahorro |
| 160 | dep_novmeahor | LND_DepositEntries | BIGINT | Novedades de depósito |
| 161 | dep_firmas | LND_DepositSignatures | INT | Firmas autorizadas |
| 162 | dep_sellos | LND_DepositSeals | INT | Sellos |
| 163 | dep_cajeros | LND_Cashiers | INT | Cajeros |
| 164 | dep_basecaja | LND_CashBases | BIGINT | Base de caja |
| 165 | dep_checanje | LND_CheckClearing | BIGINT | Cheques en canje |
| 166 | dep_talonario | LND_Checkbooks | INT | Talonarios |
| 167 | dep_maeaud | LND_DepositAudit | BIGINT | Auditoría depósitos |

### Módulo CDT_ (Certificados)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 168 | cdt_maecdats | CDT_Certificates | INT | Maestro CDTs |
| 169 | cdt_novcdats | CDT_CertificateEntries | BIGINT | Novedades CDTs |
| 170 | cdt_parame58 | CDT_Parameters | INT | Parámetros CDT |
| 171 | cdt_tasasplazos | CDT_RatesByTerm | INT | Tasas por plazo |
| 172 | cdt_asoreferencia | CDT_AssociateReferences | INT | Referencias asociado CDT |
| 173 | cdt_maeaud | CDT_Audit | BIGINT | Auditoría CDTs |
| 174 | cdt_paramaud | CDT_ParameterAudit | BIGINT | Auditoría parámetros |

### Módulo DEB_ (Tarjeta Débito)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 175 | deb_maetarj | DEB_Cards | INT | Maestro tarjetas débito |
| 176 | deb_movto | DEB_Transactions | BIGINT | Movimientos tarjeta |
| 177 | deb_parconv | DEB_AgreementParameters | INT | Parámetros convenios |
| 178 | deb_pardatafonos | DEB_PosTerminals | INT | Datáfonos |
| 179 | deb_pardiario | DEB_DailyParameters | INT | Parámetros diarios |
| 180 | deb_enpacto | DEB_Agreements | INT | Pactos/convenios |
| 181 | deb_enpactors | DEB_AgreementMembers | INT | Miembros convenio |

### Módulo PAY_ (Nómina)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 182 | nom_empleados | PAY_Employees | INT | Empleados (datos laborales, FK a COR_People) |
| 183 | nom_cptos | PAY_PayrollConcepts | INT | Conceptos de nómina |
| 184 | nom_liqplan | PAY_PayrollPlanLiquidations | BIGINT | Liquidación planilla |
| 185 | nom_movtos | PAY_PayrollTransactions | BIGINT | Movimientos nómina |
| 186 | nom_novedad | PAY_PayrollEntries | BIGINT | Novedades nómina |
| 187 | nom_novsalario | PAY_SalaryChanges | BIGINT | Novedades salariales |
| 188 | nom_perpagos | PAY_PayPeriods | INT | Períodos de pago |
| 189 | nom_ausentismos | PAY_Absences | BIGINT | Ausentismos |
| 190 | nom_libranzas | PAY_DirectDebits | BIGINT | Libranzas |
| 191 | nom_cesantias | PAY_SeveranceProviders | INT | Fondos de cesantías |
| 192 | nom_antcesantia | PAY_SeveranceHistory | BIGINT | Histórico cesantías |
| 193 | nom_liqvac | PAY_VacationLiquidations | BIGINT | Liquidación vacaciones |
| 194 | nom_preliq | PAY_PreLiquidations | BIGINT | Pre-liquidaciones |
| 195 | nom_respreliq | PAY_PreLiquidationResponses | BIGINT | Respuestas pre-liquidación |
| 196 | nom_maeliqemp | PAY_EmployeeLiquidationMasters | BIGINT | Maestro liquidación empleado |
| 197 | nom_detliqemp | PAY_EmployeeLiquidationDetails | BIGINT | Detalle liquidación empleado |
| 198 | nom_contpla | PAY_AccountingEntries | BIGINT | Contabilización planilla |
| 199 | nom_cuentas | PAY_ConceptAccounts | INT | Cuentas por concepto |
| 200 | nom_sallib | PAY_BookBalances | BIGINT | Saldos de libro |
| 201 | nom_cauret | PAY_WithholdingCauses | INT | Causas retención |
| 202 | nom_parretfte | PAY_WithholdingParameters | INT | Parámetros retención fuente |
| 203 | nom_eps | PAY_HealthInsuranceProviders | INT | EPS |
| 204 | nom_arp | PAY_WorkRiskProviders | INT | ARL/ARP |
| 205 | nom_arptarifa | PAY_WorkRiskRates | INT | Tarifas ARL |
| 206 | nom_pensiones | PAY_PensionProviders | INT | Fondos de pensiones |
| 207 | nom_impcert | PAY_TaxCertificates | BIGINT | Certificados tributarios |
| 208 | nom_parautapo | PAY_AutoContributionParams | INT | Parámetros auto-aporte |

### Módulo INV_ (Inventario)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 209 | inv_productos | INV_Products | INT | Productos |
| 210 | inv_grupos | INV_ProductGroups | INT | Grupos de producto |
| 211 | inv_Grupo_Primario | INV_PrimaryGroups | INT | Grupos primarios |
| 212 | inv_GrupoSecundario | INV_SecondaryGroups | INT | Grupos secundarios |
| 213 | inv_movtos | INV_Transactions | BIGINT | Movimientos inventario |
| 214 | inv_movtos_orden | INV_OrderTransactions | BIGINT | Movimientos orden |
| 215 | inv_tipomovtos | INV_TransactionTypes | INT | Tipos de movimiento |
| 216 | inv_facturas | INV_Invoices | BIGINT | Facturas |
| 217 | inv_docs | INV_Documents | BIGINT | Documentos |
| 218 | inv_docs_orden | INV_OrderDocuments | BIGINT | Documentos orden |
| 219 | inv_precios | INV_Prices | INT | Precios |
| 220 | inv_tipolistas | INV_PriceListTypes | INT | Tipos lista de precios |
| 221 | inv_dstos | INV_Discounts | BIGINT | Descuentos |
| 222 | inv_tipodstos | INV_DiscountTypes | INT | Tipos de descuento |
| 223 | inv_bodegas | INV_Warehouses | INT | Bodegas |
| 224 | inv_ubicacion | INV_Locations | INT | Ubicaciones |
| 225 | inv_puntos | INV_SalesPoints | INT | Puntos de venta |
| 226 | inv_turnos | INV_Shifts | INT | Turnos |
| 227 | inv_cuentas | INV_ProductAccounts | INT | Cuentas por producto/movimiento |
| 228 | inv_cuentasiva | INV_VatAccounts | INT | Cuentas IVA |
| 229 | inv_invfisico | INV_PhysicalInventory | BIGINT | Inventario físico |
| 230 | inv_param_comisiones | INV_CommissionParameters | INT | Parámetros comisiones |
| 231 | inv_param_comisiones_Precios | INV_CommissionPriceParams | INT | Parámetros comisión precios |
| 232 | inv_Vendedor | INV_Salespeople | INT | Vendedores |

### Módulo TRS_ (Tesorería)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 233 | TES_CHEQUES | TRS_Checks | BIGINT | Cheques |
| 234 | TES_CPTOS | TRS_Concepts | INT | Conceptos tesorería |
| 235 | TES_FACTURA | TRS_Invoices | BIGINT | Facturas tesorería |

### Módulo SEC_ (Seguridad)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 236 | sys_sasusu | SEC_Users | INT | Usuarios (reemplaza con campos modernos) |
| 237 | sys_menusu | SEC_UserMenuAccess | INT | Acceso a menú por usuario |
| 238 | sys_programa | SEC_Modules | INT | Módulos del sistema |
| 239 | sys_ComAsigna | SEC_UserAssignments | INT | Asignaciones de usuario |
| 240 | (nueva) | SEC_Roles | INT | Roles |
| 241 | (nueva) | SEC_Permissions | INT | Permisos |
| 242 | (nueva) | SEC_RolePermissions | INT | Roles-Permisos |
| 243 | (nueva) | SEC_UserRoles | INT | Usuarios-Roles |
| 244 | (nueva) | SEC_RefreshTokens | BIGINT | Tokens JWT |
| 245 | (nueva) | SEC_UserSessions | BIGINT | Sesiones activas |
| 246 | (nueva) | SEC_LoginAttempts | BIGINT | Intentos de login |

### Módulo AUD_ (Auditoría)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 247 | sys_ciaaud | AUD_CompanyChanges | BIGINT | Cambios en compañía |
| 248 | sys_sasusuaud | AUD_UserChanges | BIGINT | Cambios en usuarios |
| 249 | sys_compro02aud | AUD_VoucherTypeChanges | BIGINT | Cambios comprobantes |
| 250 | sys_masaud | AUD_MasterChanges | BIGINT | Cambios maestros |
| 251 | sys_menuaud | AUD_MenuChanges | BIGINT | Cambios menú |
| 252 | sys_periodoAud | AUD_PeriodChanges | BIGINT | Cambios períodos |
| 253 | sys_ComAsignaAud | AUD_AssignmentChanges | BIGINT | Cambios asignaciones |
| 254 | cnt_maecuenAud | AUD_AccountChanges | BIGINT | Cambios plan cuentas |
| 255 | cnt_movaud | AUD_JournalChanges | BIGINT | Cambios movimientos contables |
| 256 | cop_movaud | AUD_PortfolioTransactionChanges | BIGINT | Cambios movimientos cartera |
| 257 | cop_mcaaud | AUD_PortfolioMasterChanges | BIGINT | Cambios maestro cartera |
| 258 | cop_moraud | AUD_DefaultChanges | BIGINT | Cambios mora |
| 259 | cop_ahoraud | AUD_SavingsChanges | BIGINT | Cambios ahorro |
| 260 | (nueva) | AUD_AuditReferences | BIGINT | Referencias a MongoDB |

### Módulo WEB_ (Web/Online)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 261 | web_solcred | WEB_LoanApplications | BIGINT | Solicitudes crédito online |
| 262 | web_solaux | WEB_AuxiliaryApplications | BIGINT | Solicitudes aux online |
| 263 | web_solafi | WEB_AffiliationApplications | BIGINT | Solicitudes afiliación online |
| 264 | web_maeser | WEB_Services | INT | Servicios web |
| 265 | web_extras | WEB_ExtraPayments | BIGINT | Extras online |
| 266 | web_actdatos | WEB_DataUpdates | BIGINT | Actualización datos online |

### Módulo ADM_ (Administración Multi-tenant)

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 267 | (nueva) | ADM_Tenants | INT | Tenants |
| 268 | (nueva) | ADM_Subscriptions | INT | Suscripciones |
| 269 | (nueva) | ADM_TenantSettings | INT | Configuración por tenant |

### Tablas restantes

| # | Tabla Original | Tabla Nueva | Tipo PK | Justificación |
|---|---|---|---|---|
| 270 | lin_consulta | LND_OnlineQueries | BIGINT | Consultas en línea (ya tenía IDENTITY) |

---

## SECCIÓN 6: COLUMNAS RENOMBRADAS (Top 30 tablas)

### 1. COR_People (antes: sys_maenit + cnt_nit)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Origen | Notas |
|---|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY(1,1) | - | PK surrogate |
| (nueva) | PublicId | - | uniqueidentifier | - | GUID pública, UNIQUE |
| CODIGOTER | LegacyCode | varchar(14) | nvarchar(20) | sys_maenit | Para migración, indexado |
| NIT | IdentificationNumber | varchar(14) | nvarchar(20) | sys_maenit/cnt_nit | Documento identidad |
| NIT_CHEQUEO | CheckDigit | varchar(1) | nvarchar(1) | sys_maenit | Dígito verificación |
| TIPO_NIT | IdentificationType | varchar(1) | nvarchar(5) | sys_maenit | CC, NIT, CE, PP, TI |
| tipo_persona | PersonType | varchar(1) | nvarchar(1) | cnt_nit | N=Natural, J=Jurídica |
| NATJUR | IsNaturalPerson | smallint | bit | sys_maenit | Computed from PersonType |
| APELLIDO | LastName | varchar(100) | nvarchar(150) | sys_maenit | Ampliada |
| NOMBRE | FirstName | varchar(100) | nvarchar(150) | sys_maenit | Ampliada |
| RAZON_SOCIAL | BusinessName | varchar(100) | nvarchar(200) | cnt_nit | Para personas jurídicas |
| (nueva) | FullName | - | AS (FirstName + ' ' + LastName) | - | Columna computada |
| DIRECCION | Address | varchar(60) | nvarchar(250) | sys_maenit | Ampliada |
| TELEFONO1 | Phone | varchar(30) | nvarchar(30) | sys_maenit | |
| TELEFONO2 | Phone2 | varchar(30) | nvarchar(30) | sys_maenit | |
| FAX | Fax | varchar(15) | nvarchar(30) | sys_maenit | |
| MOVIL | Mobile | varchar(15) | nvarchar(30) | sys_maenit | Ampliada |
| EMAIL | Email | varchar(60) | nvarchar(200) | sys_maenit | Ampliada |
| DPTO_CIUDAD | CityId | int | int | sys_maenit | FK → COR_Cities.Id |
| SEXO | Gender | varchar(1) | nvarchar(1) | sys_maenit | M/F |
| ESTADO_CIVIL | MaritalStatus | varchar(1) | nvarchar(1) | sys_maenit | |
| FECNACEM | DateOfBirth | smalldatetime | date | sys_maenit | Solo fecha, no necesita hora |
| FecExpedicion | DocumentIssueDate | smalldatetime | date | sys_maenit | |
| EXPEDIDA | DocumentIssuedAt | varchar(20) | nvarchar(50) | sys_maenit | Lugar expedición |
| NIV_ACADE | EducationLevel | varchar(1) | nvarchar(2) | sys_maenit | |
| ESTRATO | SocioeconomicLevel | varchar(2) | nvarchar(2) | sys_maenit | |
| DIRECCION_ENVIO | MailingAddress | varchar(60) | nvarchar(250) | sys_maenit | |
| ENVIO_DIR | UseMailingAddress | varchar(1) | bit | sys_maenit | |
| TIPO_VIVIENDA | HousingType | varchar(1) | nvarchar(2) | sys_maenit | |
| VEHICULO | HasVehicle | varchar(1) | bit | sys_maenit | |
| tipovehiculo | VehicleType | int | int | sys_maenit | |
| CabezaFamilia | IsHeadOfHousehold | varchar(1) | bit | sys_maenit | |
| AUTORREFTE | IsWithholdingAgent | varchar(1) | bit | cnt_nit | Auto-retenedor fte |
| AUTORTEICA | IsIcaWithholdingAgent | varchar(1) | bit | cnt_nit | Auto-retenedor ICA |
| REGIMEN | TaxRegime | varchar(1) | nvarchar(2) | cnt_nit | |
| gran_contribu | IsLargeTaxpayer | varchar(1) | bit | cnt_nit | Gran contribuyente |
| TIPO_ICA | IcaTaxType | varchar(3) | nvarchar(5) | cnt_nit | |
| TASA_ICA | IcaTaxRate | decimal(10,5) | decimal(10,5) | cnt_nit | |
| DIAS_PAGO | PaymentDays | smallint | int | cnt_nit | |
| (nueva) | IsAssociate | - | bit DEFAULT 0 | - | Flag de rol |
| (nueva) | IsEmployee | - | bit DEFAULT 0 | - | Flag de rol |
| (nueva) | IsSupplier | - | bit DEFAULT 0 | - | Flag de rol |
| (nueva) | IsCustomer | - | bit DEFAULT 0 | - | Flag de rol |
| (nueva) | IsCodebtor | - | bit DEFAULT 0 | - | Flag de rol |
| ESTADO | Status | varchar(1) | nvarchar(2) | sys_maenit | |
| (nueva) | CreatedAt | - | datetime2 | - | Estándar |
| (nueva) | CreatedBy | - | nvarchar(100) | - | Estándar |
| (nueva) | UpdatedAt | - | datetime2 | - | Estándar |
| (nueva) | UpdatedBy | - | nvarchar(100) | - | Estándar |
| (nueva) | IsDeleted | - | bit DEFAULT 0 | - | Soft delete |

### 2. LND_LoanPortfolios (antes: cop_maecar)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| (nueva) | PublicId | - | uniqueidentifier | GUID pública |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| LINCRED | CreditLineId | int | int | FK → LND_CreditLineParameters.Id |
| NUMERO | PortfolioNumber | bigint | bigint | Número secuencial del crédito |
| (PK compuesta) | - | 3 cols | UNIQUE(PersonId, CreditLineId, PortfolioNumber) | Constraint de unicidad |
| NIT | IdentificationNumber | varchar(14) | nvarchar(20) | Desnormalizado para búsqueda |
| FECSOLIC | ApplicationDate | smalldatetime | date | Fecha solicitud |
| FECAPROB | ApprovalDate | smalldatetime | date | Fecha aprobación |
| FECFACT | DisbursementDate | smalldatetime | date | Fecha desembolso |
| FECDESC | DiscountStartDate | smalldatetime | date | Inicio descuento |
| FECULTCAU | LastAccrualDate | smalldatetime | date | Última causación |
| FECULTPAGO | LastPaymentDate | smalldatetime | date | Último pago |
| FECULTMORA | LastDefaultDate | smalldatetime | date | Última mora |
| FECVEMTO | MaturityDate | smalldatetime | date | Vencimiento |
| FECCIERRE | ClosingDate | smalldatetime | date | Cierre |
| PLAZO | TermMonths | smallint | int | Plazo en meses |
| VLRSOLICITUD | RequestedAmount | decimal(17,2) | decimal(18,2) | Monto solicitado |
| VALOROB | ApprovedAmount | decimal(17,2) | decimal(18,2) | Monto aprobado |
| SALDOT | CurrentBalance | decimal(17,2) | decimal(18,2) | Saldo actual |
| CUOTA | InstallmentAmount | decimal(17,2) | decimal(18,2) | Valor cuota |
| TASAINT | InterestRate | decimal(10,6) | decimal(10,6) | Tasa de interés |
| CICLOD | PaymentCycle | varchar(1) | nvarchar(2) | Ciclo de pago |
| PERIODD | PaymentPeriodicity | varchar(1) | nvarchar(2) | Periodicidad |
| CLACUO | InstallmentType | varchar(1) | nvarchar(2) | Clase de cuota |
| CLASEI | InterestType | varchar(1) | nvarchar(2) | Clase de interés |
| CLASEGAR | GuaranteeType | varchar(2) | nvarchar(5) | Tipo garantía |
| CLADES | DeductionType | varchar(1) | nvarchar(2) | Clase descuento |
| CUOPAG | PaidInstallments | smallint | int | Cuotas pagadas |
| CUOPENDI | PendingInstallments | decimal(5,2) | decimal(5,2) | Cuotas pendientes |
| TASAADM | AdminFeeRate | decimal(12,5) | decimal(12,5) | Tasa administración |
| TASASEG | InsuranceRate | decimal(10,5) | decimal(10,5) | Tasa seguro |
| DIASMORA | DaysOverdue | smallint | int | Días en mora |
| CATEGORIA | Category | varchar(1) | nvarchar(2) | Categoría riesgo |
| CODEUDOR1..4 | - | varchar(14) | - | Migran a LND_ApplicationCodebtors |
| AGENCIA | BranchId | varchar(4) | int | FK → COR_Branches.Id |
| CCOSTO | CostCenterId | varchar(8) | int | FK → COR_CostCenters.Id |
| USUARIO | CreatedBy | varchar(14) | nvarchar(100) | Columna estándar |
| FECHA_GRABA | CreatedAt | smalldatetime | datetime2 | Columna estándar |

### 3. ACC_ChartOfAccounts (antes: cnt_maecuen — solo metadatos)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| (nueva) | PublicId | - | uniqueidentifier | GUID |
| CUENTA | AccountCode | varchar(12) | nvarchar(15) | UNIQUE INDEX |
| NATURA | Nature | varchar(1) | nvarchar(1) | D=Débito, C=Crédito |
| NOMBRE | Name | varchar(60) | nvarchar(150) | |
| NIVEL | Level | varchar(1) | tinyint | 1-6 |
| TASA | Rate | decimal(7,4) | decimal(7,4) | |
| AUX_DOMTO | RequiresDocument | varchar(1) | bit | |
| MANE_CENCOS | ManagesCostCenter | varchar(1) | bit | |
| TERCERO | RequiresThirdParty | varchar(1) | bit | |
| ACTOPERA | IsOperationalAsset | varchar(1) | bit | |
| APLI_CARTCOOPE | AppliesToLending | varchar(1) | bit | |
| APLI_AHOR_CDT | AppliesToSavingsCDT | varchar(1) | bit | |
| APLI_INVENTA | AppliesToInventory | varchar(1) | bit | |
| APLI_TESORERI | AppliesToTreasury | varchar(1) | bit | |
| APLI_NOMINA | AppliesToPayroll | varchar(1) | bit | |
| APLI_CONTAB | AppliesToAccounting | varchar(1) | bit | |
| APLI_FACTURAC | AppliesToInvoicing | varchar(1) | bit | |
| estado | Status | int | int | |
| FlujodeCaja | CashFlowCode | varchar(2) | nvarchar(5) | |
| tipoRetencion | WithholdingType | char(1) | nvarchar(2) | |
| (eliminadas) | - | DEB_ENE...CRE_DIC | - | Migran a ACC_AccountBalances |

### 4. ACC_JournalEntries (antes: cnt_movimto)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| SECUENCIA | Id | int IDENTITY | BIGINT IDENTITY | PK (ya tenía IDENTITY, se amplía a BIGINT) |
| (nueva) | PublicId | - | uniqueidentifier | GUID |
| COMPRONTE | VoucherTypeCode | varchar(4) | nvarchar(10) | |
| NUMERO_DOMTO | DocumentNumber | bigint | bigint | |
| CUENTA | AccountId | varchar(12) | int | FK → ACC_ChartOfAccounts.Id |
| NIT | PersonId | varchar(14) | int | FK → COR_People.Id |
| FECHA_MOVTO | TransactionDate | smalldatetime | date | |
| VLR_DEBITO | DebitAmount | decimal(17,2) | decimal(18,2) | |
| VLR_CREDITO | CreditAmount | decimal(17,2) | decimal(18,2) | |
| CENCOS | CostCenterId | varchar(8) | int | FK → COR_CostCenters.Id |
| AGENCIA | BranchId | varchar(4) | int | FK → COR_Branches.Id |
| DETALLE | Description | varchar(80) | nvarchar(200) | |

### 5. LND_Transactions (antes: cop_movimto)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| SECUENCIA | Id | int IDENTITY | BIGINT IDENTITY | PK (ya tenía IDENTITY) |
| (nueva) | PublicId | - | uniqueidentifier | GUID |
| COMPRONTE | VoucherTypeCode | varchar(4) | nvarchar(10) | |
| NUMERO_DOMTO | DocumentNumber | bigint | bigint | |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| LINCRED | CreditLineId | int | int | FK → LND_CreditLineParameters.Id |
| NUMERO | LoanPortfolioId | bigint | int | FK → LND_LoanPortfolios.Id |
| CUENTA | AccountId | varchar(12) | int | FK → ACC_ChartOfAccounts.Id |
| FECHA_MOVTO | TransactionDate | smalldatetime | date | |
| VLR_DEBITO | DebitAmount | decimal(17,2) | decimal(18,2) | |
| VLR_CREDITO | CreditAmount | decimal(17,2) | decimal(18,2) | |
| COD_MOVTO | TransactionCodeId | varchar(2) | int | FK → LND_TransactionCodes.Id |
| TASA_INT | InterestRate | decimal(10,6) | decimal(10,6) | |
| detalle | Description | varchar(80) | nvarchar(200) | |
| USUARIO | CreatedBy | varchar(14) | nvarchar(100) | |
| FECHA_SYSTEMA | CreatedAt | smalldatetime | datetime2 | |

### 6. LND_PendingInstallments (antes: cop_cuopen)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| (nueva) | PublicId | - | uniqueidentifier | GUID |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| LINCRED | CreditLineId | int | int | |
| NUMERO | LoanPortfolioId | bigint | int | FK → LND_LoanPortfolios.Id |
| NUMERO_CUOTA | InstallmentNumber | smallint | int | |
| FECHA_VEMTO | DueDate | smalldatetime | date | |
| VLR_CAPITAL | PrincipalAmount | decimal(17,2) | decimal(18,2) | |
| VLR_INTERES | InterestAmount | decimal(17,2) | decimal(18,2) | |
| VLR_SEGURO | InsuranceAmount | decimal(17,2) | decimal(18,2) | |
| VLR_ADMON | AdminFeeAmount | decimal(17,2) | decimal(18,2) | |
| VLR_EXTRAS | ExtraAmount | decimal(17,2) | decimal(18,2) | |
| ESTADO | Status | varchar(1) | nvarchar(2) | P=Pendiente, C=Cancelada |

### 7-10. PAY_Employees, LND_CreditLineParameters, LND_SavingsAccounts, INV_Products

*(Siguen el mismo patrón: PK INT/BIGINT IDENTITY, PublicId GUID, FKs por Id, varchar→nvarchar, smalldatetime→date/datetime2)*

### 11. COR_Associates (antes: campos de sys_maenit)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK |
| (nueva) | PublicId | - | uniqueidentifier | GUID |
| (nueva) | PersonId | - | int | FK → COR_People.Id, UNIQUE |
| FECHA_INGRESO | JoinDate | smalldatetime | date | |
| TASA_APORTE | ContributionRate | decimal(5,2) | decimal(5,2) | |
| EMPRESA | EmployerCompanyId | varchar(4) | int | FK → COR_EmployerCompanies.Id |
| AGENCIA | BranchId | varchar(4) | int | FK → COR_Branches.Id |
| CENCOSTO | CostCenterId | varchar(8) | int | FK → COR_CostCenters.Id |
| SECCION_EMPRESA | SectionId | varchar(4) | int | FK → COR_Sections.Id |
| DIA_CORTE | CutoffDay | smallint | tinyint | |
| ESTADO | Status | varchar(1) | nvarchar(2) | A=Activo, R=Retirado |
| FECHA_RETIRO | WithdrawalDate | smalldatetime | date | |
| MOTIVO_RETIRO | WithdrawalReasonId | varchar(4) | int | FK → COR_WithdrawalReasons.Id |
| FECHA_REINGRESO | RejoinDate | smalldatetime | date | |
| CALIFI_CATEG | CategoryRating | varchar(1) | nvarchar(2) | |
| ASESOR | AdvisorId | varchar(14) | int | FK → COR_Advisors.Id |
| PERIODO_DESTO | DeductionPeriod | varchar(1) | nvarchar(2) | |
| CLASE_DESTO | DeductionType | varchar(1) | nvarchar(2) | |
| ESCALAFON | Rank | varchar(2) | nvarchar(5) | |
| REFERIDO | ReferredBy | varchar(14) | int | FK → COR_People.Id |
| COBROJUR | IsInLegalCollection | smallint | bit | |
| CONTRACTO | ContractNumber | smallint | int | |
| VENCONTRACTO | ContractExpiryDate | smalldatetime | date | |
| comite | CommitteeId | varchar(4) | int | FK → COR_Committees.Id |
| COPZONA | ZoneCode | varchar(8) | nvarchar(10) | |
| pignora_aport | ContributionPledged | varchar(1) | bit | |

### 12-30. Tablas restantes del top 30

Las tablas `LND_CreditLineParameters`, `LND_Documents`, `LND_DefaultRecords`, `ACC_AccountBalances`, `ACC_Documents`, `PAY_PayrollConcepts`, `PAY_PayrollPlanLiquidations`, `PAY_PayrollTransactions`, `INV_Products`, `INV_Transactions`, `INV_Invoices`, `CDT_Certificates`, `CDT_CertificateEntries`, `DEB_Cards`, `DEB_Transactions`, `TRS_Checks`, `SEC_Users`, `COR_Beneficiaries` siguen el mismo patrón de transformación:

1. Agregar `Id` INT/BIGINT IDENTITY como PK clustered
2. Agregar `PublicId` uniqueidentifier con UNIQUE INDEX
3. Convertir referencias varchar a FKs INT (→ tabla padre.Id)
4. Convertir varchar → nvarchar
5. Convertir smalldatetime → date o datetime2
6. Agregar columnas estándar de auditoría (CreatedAt, CreatedBy, etc.)
7. Convertir PKs compuestas actuales a UNIQUE constraints
8. Eliminar columnas FILLER*

---

## SECCIÓN 7: ESTRATEGIA DE MIGRACIÓN DE LLAVES

### Proceso general para migrar PKs compuestas

**Ejemplo detallado: cop_maecar → LND_LoanPortfolios**

```
PASO 1: Crear tabla nueva con IDENTITY
────────────────────────────────────────
CREATE TABLE LND_LoanPortfolios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PublicId uniqueidentifier DEFAULT NEWID() NOT NULL,
    PersonId INT NOT NULL,  -- FK a COR_People
    CreditLineId INT NOT NULL,
    PortfolioNumber BIGINT NOT NULL,
    ...columnas de negocio...,
    -- Columnas legacy para rastreo
    LegacyCodigoTer nvarchar(14) NULL,
    CONSTRAINT UQ_LoanPortfolio UNIQUE (PersonId, CreditLineId, PortfolioNumber)
);

PASO 2: Insertar datos con mapeo
────────────────────────────────
-- Primero, COR_People ya debe existir con mapeo CODIGOTER → Id
INSERT INTO LND_LoanPortfolios (PersonId, CreditLineId, PortfolioNumber, ..., LegacyCodigoTer)
SELECT
    p.Id AS PersonId,
    m.LINCRED AS CreditLineId,
    m.NUMERO AS PortfolioNumber,
    ...,
    m.CODIGOTER AS LegacyCodigoTer
FROM cop_maecar m
INNER JOIN COR_People p ON m.CODIGOTER = p.LegacyCode;

PASO 3: Crear tabla de mapeo temporal
─────────────────────────────────────
CREATE TABLE #MapLoanPortfolio (
    OldCodigoTer varchar(14),
    OldLincred int,
    OldNumero bigint,
    NewLoanPortfolioId int
);

INSERT INTO #MapLoanPortfolio
SELECT LegacyCodigoTer, CreditLineId, PortfolioNumber, Id
FROM LND_LoanPortfolios;

PASO 4: Migrar tablas hijas usando el mapeo
───────────────────────────────────────────
-- Ejemplo: cop_cuopen → LND_PendingInstallments
INSERT INTO LND_PendingInstallments (LoanPortfolioId, InstallmentNumber, ...)
SELECT
    map.NewLoanPortfolioId,
    c.NUMERO_CUOTA,
    ...
FROM cop_cuopen c
INNER JOIN #MapLoanPortfolio map
    ON c.CODIGOTER = map.OldCodigoTer
    AND c.LINCRED = map.OldLincred
    AND c.NUMERO = map.OldNumero;

PASO 5: Mantener columnas de negocio para búsquedas directas
────────────────────────────────────────────────────────────
-- LND_PendingInstallments también tiene PersonId y CreditLineId
-- como columnas indexadas para facilitar queries sin JOIN al padre
```

### Orden de migración recomendado

```
1. COR_People         ← sys_maenit + cnt_nit (base de todo)
2. COR_Branches       ← sys_agencia
3. COR_CostCenters    ← sys_cencos + nom_cencos + cnt_cencos
4. COR_Cities         ← sys_ciudad57
5. COR_Banks          ← sys_banco03
6. COR_EmployerCompanies ← cop_empresa13 + nom_empresas
7. COR_Associates     ← campos de sys_maenit
8. ACC_ChartOfAccounts ← cnt_maecuen (metadatos)
9. ACC_VoucherTypes   ← sys_compro02
10. LND_CreditLineParameters ← cop_concar12
11. LND_LoanPortfolios ← cop_maecar (genera mapeo)
12. LND_Transactions  ← cop_movimto (usa mapeo)
13. LND_PendingInstallments ← cop_cuopen (usa mapeo)
14. LND_SavingsAccounts ← cop_maeahor + dep_maeahor
15. ACC_JournalEntries ← cnt_movimto
16. ACC_AccountBalances ← cnt_salage (normalizado)
17. PAY_Employees     ← nom_empleados
18. PAY_* (restantes) ← nom_*
19. INV_*             ← inv_*
20. CDT_*             ← cdt_*
21. DEB_*             ← deb_*
22. TRS_*             ← TES_*
23. SEC_*             ← sys_sasusu, sys_menusu
24. AUD_*             ← *aud
25. WEB_*             ← web_*
```

### Consideraciones clave de migración

1. **Columna LegacyCode:** Toda tabla nueva mantiene una columna `LegacyXxx` nullable para rastreo durante la migración. Se puede eliminar 6 meses después de producción estable.

2. **FKs de varchar a INT:** Requieren tabla de mapeo temporal `(old_varchar_key → new_int_id)`. Se crean para COR_People, LND_LoanPortfolios, ACC_ChartOfAccounts, y todas las tablas con PK varchar.

3. **PKs compuestas de 5+ columnas (28 tablas):** Son las más complejas. El mapeo temporal debe incluir TODAS las columnas de la PK original. Se recomienda crear índices en la tabla temporal para performance.

4. **Datos desnormalizados (cnt_maecuen saldos mensuales):** Se normalizan en la migración. Un query INSERT...SELECT con UNPIVOT transforma las 24 columnas de saldo en filas de ACC_AccountBalances.

---

## SECCIÓN 8: RESUMEN ESTADÍSTICO

| Métrica | Valor |
|---|---|
| **Tablas originales** | 270 |
| **Tablas eliminadas** | 12 |
| **Tablas fusionadas** | 25 tablas → 12 tablas (9 grupos de fusión) |
| **Tablas nuevas** | 23 |
| **Total tablas en nuevo esquema** | **269** (270 - 12 eliminadas - 13 netas fusión + 23 nuevas + 1 ajuste) |
| | |
| **Tablas con INT PK (maestras)** | ~160 |
| **Tablas con BIGINT PK (transaccionales)** | ~109 |
| | |
| **Vistas originales** | 121 |
| **Funciones originales** | 8 |
| **Foreign keys originales** | 228 |
| | |
| **Problemas resueltos** | |
| Tablas sin PK | 34 → 0 |
| PKs compuestas | 122 → 0 (convertidas a UNIQUE + surrogate INT/BIGINT) |
| PKs varchar | ~80 → 0 (todas INT/BIGINT IDENTITY) |
| varchar sin Unicode | ~95% → 0% (todo nvarchar) |
| smalldatetime | ~100+ cols → 0 (todo date/datetime2) |
| Columnas FILLER | ~20 tablas → 0 |
| Tabla monolítica sys_maenit | 1 × 130 cols → COR_People + COR_Associates + COR_Spouses + COR_PeopleFinancial |
| Saldos desnormalizados | 2 tablas × 24+ cols → ACC_AccountBalances normalizada |
| Datos persona duplicados | cnt_nit ↔ sys_maenit → COR_People unificada |

### Distribución por módulo (nuevo esquema)

| Módulo | Prefijo | Tablas | % |
|---|---|---|---|
| Core/Sistema | COR_ | 42 | 15.6% |
| Lending/Cartera | LND_ | 83 | 30.9% |
| Accounting/Contabilidad | ACC_ | 33 | 12.3% |
| Payroll/Nómina | PAY_ | 27 | 10.0% |
| Inventory/Inventario | INV_ | 24 | 8.9% |
| Security/Seguridad | SEC_ | 11 | 4.1% |
| Audit/Auditoría | AUD_ | 14 | 5.2% |
| CDT/Certificados | CDT_ | 7 | 2.6% |
| Debit Cards | DEB_ | 7 | 2.6% |
| Web/Online | WEB_ | 6 | 2.2% |
| Treasury/Tesorería | TRS_ | 3 | 1.1% |
| Admin/Multi-tenant | ADM_ | 3 | 1.1% |
| Otros | LND_ (consultas) | 1 | 0.4% |
| **TOTAL** | | **269** | **100%** |

---

> **Próximos pasos:**
> 1. Revisión de esta propuesta por el equipo
> 2. Validación de las fusiones y eliminaciones
> 3. Generación del DDL completo (CREATE TABLE + constraints + indexes)
> 4. Scripts de migración de datos con tablas de mapeo
> 5. Generación de vistas de compatibilidad (nombres viejos → tablas nuevas)
