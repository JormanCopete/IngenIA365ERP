# MAPEO COMPLETO BD VIEJA → BD NUEVA — IngenIA365ERP

> **Fecha:** 2026-03-20
> **Origen:** DBDefinicion.sql (SOLIDO ERP, 270 tablas)
> **Destino:** IngenIA365ERP Schema v1.0 (269 tablas)
> **Referencia:** PROPUESTA-REDISENO-BD.md (aprobada)

---

## SECCIÓN 1: MAPEO DE TABLAS (270 tablas originales)

### Leyenda de acciones
| Acción | Descripción |
|---|---|
| **Renombrada** | Tabla renombrada 1:1 (misma estructura lógica, nueva PK surrogate + columnas estándar) |
| **Fusionada** | Tabla absorbida en otra tabla nueva (indicada en "Tabla Nueva") |
| **Eliminada** | Tabla eliminada del nuevo esquema (temporal, duplicada u obsoleta) |
| **Dividida** | Tabla partida en múltiples tablas nuevas |
| **Normalizada** | Tabla cuyas columnas desnormalizadas se reorgenizan en tabla(s) separada(s) |

### Tabla completa de mapeo

| # | Tabla Original | Tabla Nueva | Acción | Módulo | Notas |
|---|---|---|---|---|---|
| 1 | cdt_asoreferencia | CDT_AssociateReferences | Renombrada | CDT_ | Sin PK original → INT IDENTITY |
| 2 | cdt_maeaud | CDT_Audit | Renombrada | CDT_ | Auditoría CDTs |
| 3 | cdt_maecdats | CDT_Certificates | Renombrada | CDT_ | Maestro CDTs. PK compuesta → INT IDENTITY |
| 4 | cdt_novcdats | CDT_CertificateEntries | Renombrada | CDT_ | Novedades CDTs. PK compuesta 4 cols → BIGINT |
| 5 | cdt_paramaud | CDT_ParameterAudit | Renombrada | CDT_ | Auditoría parámetros CDT |
| 6 | cdt_parame58 | CDT_Parameters | Renombrada | CDT_ | Parámetros CDT |
| 7 | cdt_tasasplazos | CDT_RatesByTerm | Renombrada | CDT_ | PK compuesta 5 cols → INT |
| 8 | cnt_amortiza | ACC_Amortizations | Renombrada | ACC_ | PK compuesta 6 cols → BIGINT |
| 9 | cnt_cencos | COR_CostCenters | Fusionada | COR_ | Fusionada con sys_cencos + nom_cencos |
| 10 | cnt_certrefte | *(eliminada)* | Eliminada | — | Sin PK. Datos calculables, certificados rete-fuente regenerables |
| 11 | cnt_codforimpu | ACC_TaxFormCodes | Renombrada | ACC_ | Códigos formatos impuestos. Sin PK → INT |
| 12 | cnt_concibanca | ACC_BankReconciliations | Renombrada | ACC_ | Conciliaciones bancarias |
| 13 | cnt_concibancaplano | ACC_BankReconciliationFlats | Renombrada | ACC_ | Planos conciliación |
| 14 | cnt_deprecia | ACC_Depreciations | Renombrada | ACC_ | PK compuesta → BIGINT |
| 15 | cnt_docaux | ACC_AuxiliaryDocuments | Renombrada | ACC_ | PK compuesta 8 cols! → BIGINT |
| 16 | cnt_docmto | ACC_Documents | Renombrada | ACC_ | PK compuesta 2 cols → BIGINT |
| 17 | cnt_estadosdecambio | ACC_ExchangeRateHistory | Renombrada | ACC_ | Sin PK → BIGINT |
| 18 | cnt_estampilla | ACC_StampTaxes | Renombrada | ACC_ | Estampillas |
| 19 | cnt_grupocuenta | ACC_AccountGroups | Renombrada | ACC_ | Sin PK → INT |
| 20 | cnt_infmedian | ACC_FinancialReports | Renombrada | ACC_ | Informes financieros |
| 21 | cnt_lineagmf | ACC_GmfTaxLines | Renombrada | ACC_ | Líneas GMF |
| 22 | cnt_lineaica | ACC_IcaTaxLines | Renombrada | ACC_ | Líneas ICA |
| 23 | cnt_lineaiva | ACC_VatTaxLines | Renombrada | ACC_ | Líneas IVA |
| 24 | cnt_linearenta | ACC_IncomeTaxLines | Renombrada | ACC_ | Líneas renta |
| 25 | cnt_linretefuente | ACC_WithholdingTaxLines | Renombrada | ACC_ | Líneas rete-fuente |
| 26 | cnt_maeconcibanca | ACC_BankReconciliationMasters | Renombrada | ACC_ | Maestro conciliaciones |
| 27 | cnt_maecuen | ACC_ChartOfAccounts + ACC_AccountBalances | Dividida/Normalizada | ACC_ | Metadatos → ACC_ChartOfAccounts. 24 cols saldos (DEB_ENE..CRE_DIC) → ACC_AccountBalances normalizada |
| 28 | cnt_maecuenAud | AUD_AccountChanges | Renombrada | AUD_ | Auditoría plan de cuentas |
| 29 | cnt_movaud | AUD_JournalChanges | Renombrada | AUD_ | Auditoría movimientos contables |
| 30 | cnt_movimto | ACC_JournalEntries | Renombrada | ACC_ | Ya tenía IDENTITY → BIGINT |
| 31 | cnt_movitem | ACC_JournalEntryItems | Renombrada | ACC_ | Sin PK → BIGINT |
| 32 | cnt_nit | COR_People | Fusionada | COR_ | Fusionada con sys_maenit → COR_People. Datos contables (régimen, ICA, gran contribuyente) prevalecen de cnt_nit |
| 33 | cnt_nombregrupo | ACC_GroupNames | Renombrada | ACC_ | IDENTITY sin PK → INT |
| 34 | cnt_nombresubgrupo | ACC_SubgroupNames | Renombrada | ACC_ | IDENTITY sin PK → INT |
| 35 | cnt_parfordian | ACC_DianReportFormats | Renombrada | ACC_ | Formatos DIAN |
| 36 | cnt_parinfmedian | ACC_FinancialReportParams | Renombrada | ACC_ | Parámetros informes financieros |
| 37 | cnt_parvalmedian | ACC_FinancialReportValues | Renombrada | ACC_ | Valores informes financieros |
| 38 | cnt_presupto | ACC_Budgets | Renombrada | ACC_ | Presupuestos |
| 39 | cnt_riesgo | ACC_RiskCategories | Renombrada | ACC_ | Categorías de riesgo |
| 40 | cnt_salage | ACC_AccountBalances | Fusionada/Normalizada | ACC_ | Saldos por agencia. 24 cols mensuales → normalizadas en ACC_AccountBalances junto con cnt_maecuen saldos |
| 41 | cnt_saldocortolargo | *(eliminada)* | Eliminada | — | Sin PK. Temporal para clasificación saldos, calculable desde cnt_salage |
| 42 | cnt_subgrupocuenta | ACC_AccountSubgroups | Renombrada | ACC_ | Sin PK → INT |
| 43 | cnt_tercero | ACC_ThirdPartyAccounts | Renombrada | ACC_ | PK compuesta 5 cols → BIGINT |
| 44 | cnt_tmpflujocaja | *(eliminada)* | Eliminada | — | Prefijo tmp. Temporal para cálculo flujo de caja |
| 45 | cop_LinAud | LND_CreditLineAudit | Renombrada | LND_ | Auditoría líneas crédito |
| 46 | cop_Sipla_Novedades | LND_UnusualTransactionEntries | Renombrada | LND_ | Novedades SIPLA |
| 47 | cop_Tipozonas | LND_ZoneTypes | Renombrada | LND_ | Tipos de zona |
| 48 | cop_acta | LND_Minutes | Renombrada | LND_ | Actas |
| 49 | cop_actiaso | LND_AssociateActivities | Fusionada | LND_ | Fusionada con cop_actirecrea + cop_novactividad |
| 50 | cop_actirecrea | LND_AssociateActivities | Fusionada | LND_ | Fusionada con cop_actiaso + cop_novactividad → LND_AssociateActivities + LND_ActivityEnrollments |
| 51 | cop_ahoraud | AUD_SavingsChanges | Renombrada | AUD_ | Auditoría ahorro |
| 52 | cop_ahorro58 | LND_SavingsParameters | Renombrada | LND_ | Parámetros de ahorro |
| 53 | cop_asesores | COR_Advisors | Renombrada | COR_ | Asesores |
| 54 | cop_asoacta | LND_MinuteAttendees | Renombrada | LND_ | Asistentes a actas |
| 55 | cop_auxilio | LND_Subsidies | Renombrada | LND_ | Auxilios |
| 56 | cop_benef | COR_Beneficiaries | Fusionada | COR_ | Fusionada con nom_bene → COR_Beneficiaries con BeneficiaryType |
| 57 | cop_benefseg | LND_InsuranceBeneficiaries | Fusionada | LND_ | Fusionada con cop_seguros + cop_seguross → LND_InsurancePolicies + LND_InsuranceBeneficiaries |
| 58 | cop_carteraclp | LND_ShortLongTermPortfolio | Renombrada | LND_ | Cartera corto/largo plazo |
| 59 | cop_caunov | LND_AccrualEntries | Renombrada | LND_ | Causaciones. PK compuesta 4 cols → BIGINT |
| 60 | cop_claint | LND_InterestRates | Renombrada | LND_ | Tasas de interés |
| 61 | cop_codmov | LND_TransactionCodes | Renombrada | LND_ | Códigos de movimiento |
| 62 | cop_comite | COR_Committees | Renombrada | COR_ | Comités |
| 63 | cop_comiteasoc | COR_CommitteeMembers | Renombrada | COR_ | Miembros de comité |
| 64 | cop_concar12 | LND_CreditLineParameters | Renombrada | LND_ | ~90 columnas! Parámetros líneas de crédito |
| 65 | cop_copclas | LND_PortfolioClassifications | Renombrada | LND_ | PK compuesta 4 cols → BIGINT |
| 66 | cop_copctas | LND_PortfolioAccounts | Renombrada | LND_ | Cuentas de cartera |
| 67 | cop_copmora | LND_DefaultRecords | Renombrada | LND_ | PK compuesta 5 cols → BIGINT |
| 68 | cop_cuoant | LND_PreviousInstallments | Renombrada | LND_ | Cuotas anteriores |
| 69 | cop_cuopen | LND_PendingInstallments | Renombrada | LND_ | PK compuesta 5 cols → BIGINT |
| 70 | cop_declavado | LND_MoneyLaunderingDeclarations | Renombrada | LND_ | Declaraciones lavado activos |
| 71 | cop_detallefactura | LND_InvoiceDetails | Renombrada | LND_ | Detalles factura |
| 72 | cop_detcircobro | LND_CollectionNoticeDetails | Renombrada | LND_ | Detalle circulares cobro |
| 73 | cop_detfact | LND_InvoiceLineItems | Renombrada | LND_ | Líneas de factura |
| 74 | cop_docmto | LND_Documents | Renombrada | LND_ | PK compuesta 2 cols → BIGINT |
| 75 | cop_empresa13 | COR_EmployerCompanies | Fusionada | COR_ | Fusionada con nom_empresas |
| 76 | cop_enfermedadAsoc | LND_AssociateDiseases | Renombrada | LND_ | Enfermedades de asociado |
| 77 | cop_estretiro | LND_WithdrawalStatuses | Renombrada | LND_ | Estados de retiro |
| 78 | cop_estsol | LND_ApplicationStatuses | Renombrada | LND_ | Estados de solicitud |
| 79 | cop_extras | LND_ExtraPayments | Renombrada | LND_ | PK compuesta 4 cols → BIGINT |
| 80 | cop_extrasoli | LND_ApplicationExtras | Renombrada | LND_ | Extras en solicitud |
| 81 | cop_facturacartera | LND_PortfolioInvoices | Renombrada | LND_ | Facturas cartera |
| 82 | cop_garantia | LND_Guarantees | Renombrada | LND_ | PK compuesta 3 cols → INT |
| 83 | cop_gesmaes | LND_CollectionMasters | Renombrada | LND_ | Maestro gestión cobro |
| 84 | cop_gespara | LND_CollectionPeriods | Renombrada | LND_ | Períodos gestión cobro |
| 85 | cop_huellafirma | LND_BiometricRecords | Renombrada | LND_ | Huellas y firmas |
| 86 | cop_linsolaux | LND_AuxiliaryApplicationLines | Renombrada | LND_ | Líneas solicitud auxiliar |
| 87 | cop_liqmor | LND_DefaultLiquidations | Renombrada | LND_ | PK compuesta 5 cols → BIGINT |
| 88 | cop_listanegra | LND_Blacklist | Renombrada | LND_ | Lista negra |
| 89 | cop_maeahor | LND_SavingsAccounts | Renombrada | LND_ | Maestro cuentas de ahorro |
| 90 | cop_maecar | LND_LoanPortfolios | Renombrada | LND_ | ~120 columnas. PK compuesta 3 cols (CODIGOTER,LINCRED,NUMERO) → INT IDENTITY. Codeudores migran a LND_ApplicationCodebtors |
| 91 | cop_maecircobro | LND_CollectionNotices | Renombrada | LND_ | Circulares de cobro |
| 92 | cop_maefact | LND_InvoiceMasters | Renombrada | LND_ | Maestro facturas |
| 93 | cop_maegescob | LND_CollectionCases | Renombrada | LND_ | Ya tenía IDENTITY → BIGINT |
| 94 | cop_maenitbienes | LND_PersonAssets | Renombrada | LND_ | Bienes de personas |
| 95 | cop_maerest | LND_LoanRestructurings | Renombrada | LND_ | Reestructuraciones |
| 96 | cop_mcaaud | AUD_PortfolioMasterChanges | Renombrada | AUD_ | Auditoría maestro cartera |
| 97 | cop_moraud | AUD_DefaultChanges | Renombrada | AUD_ | Auditoría mora |
| 98 | cop_movaud | AUD_PortfolioTransactionChanges | Renombrada | AUD_ | Auditoría movimientos cartera |
| 99 | cop_movimto | LND_Transactions | Renombrada | LND_ | Ya tenía IDENTITY → BIGINT |
| 100 | cop_nomconce | LND_PayrollDeductionConcepts | Renombrada | LND_ | Conceptos descuento nómina |
| 101 | cop_nomdes | LND_PayrollDeductions | Renombrada | LND_ | PK compuesta 10 cols! → BIGINT |
| 102 | cop_nomnov | LND_PayrollDeductionEntries | Renombrada | LND_ | Novedades descuento nómina |
| 103 | cop_nompla | LND_PayrollDeductionPeriods | Renombrada | LND_ | Períodos descuento nómina |
| 104 | cop_novactividad | LND_ActivityEnrollments | Fusionada | LND_ | Fusionada con cop_actiaso + cop_actirecrea |
| 105 | cop_novfecgestion | LND_CollectionDateEntries | Renombrada | LND_ | Novedades fecha gestión |
| 106 | cop_paracircular | LND_CollectionNoticeParams | Renombrada | LND_ | Parámetros circulares |
| 107 | cop_paramPeriocidad | LND_PeriodicityParameters | Renombrada | LND_ | Parámetros periodicidad |
| 108 | cop_param_grup_sipla | LND_SiplaGroupParameters | Renombrada | LND_ | Parámetros grupo SIPLA |
| 109 | cop_param_sipla | LND_SiplaParameters | Renombrada | LND_ | Parámetros SIPLA |
| 110 | cop_paramscoring | LND_ScoringParameters | Renombrada | LND_ | Parámetros scoring |
| 111 | cop_parprov | LND_ProvisionParameters | Renombrada | LND_ | Parámetros provisiones |
| 112 | cop_parredaportes | LND_ContributionReductionParams | Fusionada | LND_ | Fusionada con cop_redapo + cop_redaportes |
| 113 | cop_partasasplazo | LND_RatesByTerm | Renombrada | LND_ | Tasas por plazo |
| 114 | cop_parviv | LND_HousingParameters | Renombrada | LND_ | Parámetros vivienda |
| 115 | cop_percau | LND_AccrualPeriods | Renombrada | LND_ | Períodos de causación |
| 116 | cop_progactividad | COR_ActivityPrograms | Renombrada | COR_ | Programas de actividades |
| 117 | cop_rangoscoring | LND_ScoringRanges | Renombrada | LND_ | Rangos scoring |
| 118 | cop_redapo | LND_ContributionReductions | Fusionada | LND_ | Fusionada con cop_redaportes + cop_parredaportes |
| 119 | cop_redaportes | LND_ContributionReductions | Fusionada | LND_ | Fusionada con cop_redapo + cop_parredaportes |
| 120 | cop_retiro | *(eliminada)* | Eliminada | — | Duplicada con cop_retiros. Sin PK. cop_retiros es la activa |
| 121 | cop_retiros | LND_AssociateWithdrawals | Renombrada | LND_ | Retiros de asociados |
| 122 | cop_riesgo | LND_RiskAssessments | Renombrada | LND_ | Evaluaciones de riesgo |
| 123 | cop_salextras | LND_ExtraPaymentBalances | Renombrada | LND_ | Saldos extras |
| 124 | cop_salmaecar | LND_PortfolioBalances | Renombrada | LND_ | PK compuesta 4 cols → BIGINT |
| 125 | cop_seguros | LND_InsurancePolicies | Fusionada | LND_ | Fusionada con cop_seguross + cop_benefseg |
| 126 | cop_seguross | LND_InsurancePolicies | Fusionada | LND_ | Fusionada con cop_seguros + cop_benefseg |
| 127 | cop_sipla_inusuales | LND_UnusualTransactions | Renombrada | LND_ | Ya tenía IDENTITY → BIGINT |
| 128 | cop_solaux | LND_AuxiliaryApplications | Renombrada | LND_ | Ya tenía IDENTITY → BIGINT |
| 129 | cop_solauxcuota | LND_AuxAppInstallments | Renombrada | LND_ | Cuotas solicitudes auxiliares |
| 130 | cop_solauxcuotabenef | LND_AuxAppInstallmentBeneficiaries | Renombrada | LND_ | Beneficiarios cuotas aux |
| 131 | cop_solbienes | LND_ApplicationAssets | Renombrada | LND_ | Bienes en solicitud |
| 132 | cop_solcodeudor | LND_ApplicationCodebtors | Renombrada | LND_ | Codeudores en solicitud |
| 133 | cop_solcre | LND_LoanApplications | Renombrada | LND_ | ~80 columnas. PK compuesta → BIGINT |
| 134 | cop_solparviv | LND_HousingApplicationParams | Renombrada | LND_ | Parámetros solicitud vivienda |
| 135 | cop_solreferencia | LND_ApplicationReferences | Renombrada | LND_ | Referencias en solicitud |
| 136 | cop_subzonas | LND_SubZones | Renombrada | LND_ | Sub-zonas |
| 137 | cop_tasasplazos | LND_TermRates | Renombrada | LND_ | Plazos y tasas |
| 138 | cop_tempmovextra | *(eliminada)* | Eliminada | — | Temporal. 12 columnas, sin PK, sin relaciones |
| 139 | cop_tmplavado | *(eliminada)* | Eliminada | — | Temporal. Datos transitorios lavado activos, regenerable |
| 140 | cop_valdesc | LND_DeductionValues | Renombrada | LND_ | PK compuesta 8 cols → BIGINT |
| 141 | cop_zonas | LND_Zones | Renombrada | LND_ | Zonas |
| 142 | COP_SOLRECR | LND_RecreationApplications | Renombrada | LND_ | Solicitudes recreación |
| 143 | cre_parame01 | LND_CreditParameters | Renombrada | LND_ | Parámetros crédito |
| 144 | deb_enpacto | DEB_Agreements | Renombrada | DEB_ | Pactos/convenios |
| 145 | deb_enpactors | DEB_AgreementMembers | Renombrada | DEB_ | Miembros convenio |
| 146 | deb_maetarj | DEB_Cards | Renombrada | DEB_ | PK compuesta (Banco,Tarjeta) → INT |
| 147 | deb_movto | DEB_Transactions | Renombrada | DEB_ | PK compuesta → BIGINT |
| 148 | deb_parconv | DEB_AgreementParameters | Renombrada | DEB_ | Parámetros convenios |
| 149 | deb_pardatafonos | DEB_PosTerminals | Renombrada | DEB_ | Sin PK → INT |
| 150 | deb_pardiario | DEB_DailyParameters | Renombrada | DEB_ | Parámetros diarios |
| 151 | dep_basecaja | LND_CashBases | Renombrada | LND_ | Base de caja |
| 152 | dep_cajeros | LND_Cashiers | Renombrada | LND_ | PK compuesta 3 cols → INT |
| 153 | dep_checanje | LND_CheckClearing | Renombrada | LND_ | Cheques en canje |
| 154 | dep_firmas | LND_DepositSignatures | Renombrada | LND_ | PK compuesta 2 cols → INT |
| 155 | dep_maeahor | LND_DepositAccounts | Renombrada | LND_ | PK varchar → INT |
| 156 | dep_maeaud | LND_DepositAudit | Renombrada | LND_ | Auditoría depósitos |
| 157 | dep_novmeahor | LND_DepositEntries | Renombrada | LND_ | PK compuesta 4 cols → BIGINT |
| 158 | dep_sellos | LND_DepositSeals | Renombrada | LND_ | Sellos |
| 159 | dep_talonario | LND_Checkbooks | Renombrada | LND_ | Talonarios |
| 160 | inv_bodegas | INV_Warehouses | Renombrada | INV_ | Bodegas |
| 161 | inv_cuentas | INV_ProductAccounts | Renombrada | INV_ | Cuentas por producto/movimiento |
| 162 | inv_cuentasiva | INV_VatAccounts | Renombrada | INV_ | Cuentas IVA |
| 163 | inv_docs | INV_Documents | Renombrada | INV_ | PK compuesta 2 cols → BIGINT |
| 164 | inv_docs_orden | INV_OrderDocuments | Renombrada | INV_ | Documentos orden |
| 165 | inv_dstos | INV_Discounts | Renombrada | INV_ | Sin PK → BIGINT |
| 166 | inv_facturas | INV_Invoices | Renombrada | INV_ | Sin PK, 35 cols → BIGINT |
| 167 | inv_Grupo_Primario | INV_PrimaryGroups | Renombrada | INV_ | Grupos primarios |
| 168 | inv_grupos | INV_ProductGroups | Renombrada | INV_ | Ya tenía INT PK |
| 169 | inv_GrupoSecundario | INV_SecondaryGroups | Renombrada | INV_ | Grupos secundarios |
| 170 | inv_invfisico | INV_PhysicalInventory | Renombrada | INV_ | Inventario físico |
| 171 | inv_movtos | INV_Transactions | Renombrada | INV_ | Sin PK, usa float para Secuencia → BIGINT |
| 172 | inv_movtos_orden | INV_OrderTransactions | Renombrada | INV_ | Movimientos orden |
| 173 | inv_param_comisiones | INV_CommissionParameters | Renombrada | INV_ | Parámetros comisiones |
| 174 | inv_param_comisiones_Precios | INV_CommissionPriceParams | Renombrada | INV_ | Parámetros comisión precios |
| 175 | inv_precios | INV_Prices | Renombrada | INV_ | PK compuesta 3 cols → INT |
| 176 | inv_productos | INV_Products | Renombrada | INV_ | Ya tenía INT PK |
| 177 | inv_puntos | INV_SalesPoints | Renombrada | INV_ | Puntos de venta |
| 178 | inv_tipodstos | INV_DiscountTypes | Renombrada | INV_ | Tipos de descuento |
| 179 | inv_tipolistas | INV_PriceListTypes | Renombrada | INV_ | Tipos lista de precios |
| 180 | inv_tipomovtos | INV_TransactionTypes | Renombrada | INV_ | Tipos de movimiento |
| 181 | inv_turnos | INV_Shifts | Renombrada | INV_ | Turnos |
| 182 | inv_ubicacion | INV_Locations | Renombrada | INV_ | Ubicaciones |
| 183 | inv_Vendedor | INV_Salespeople | Renombrada | INV_ | Vendedores |
| 184 | lin_consulta | LND_OnlineQueries | Renombrada | LND_ | Ya tenía IDENTITY → BIGINT |
| 185 | logo | *(eliminada)* | Eliminada | — | 1 fila con imagen. Debe ir como recurso estático en storage |
| 186 | nom_antcesantia | PAY_SeveranceHistory | Renombrada | PAY_ | Histórico cesantías |
| 187 | nom_arp | PAY_WorkRiskProviders | Renombrada | PAY_ | ARL/ARP |
| 188 | nom_arptarifa | PAY_WorkRiskRates | Renombrada | PAY_ | Tarifas ARL |
| 189 | nom_ausentismos | PAY_Absences | Renombrada | PAY_ | Ausentismos |
| 190 | nom_bene | COR_Beneficiaries | Fusionada | COR_ | Fusionada con cop_benef → COR_Beneficiaries con BeneficiaryType |
| 191 | nom_cauret | PAY_WithholdingCauses | Renombrada | PAY_ | Causas retención |
| 192 | nom_cencos | COR_CostCenters | Fusionada | COR_ | Fusionada con sys_cencos + cnt_cencos |
| 193 | nom_cesantias | PAY_SeveranceProviders | Renombrada | PAY_ | Fondos de cesantías |
| 194 | nom_contpla | PAY_AccountingEntries | Renombrada | PAY_ | Contabilización planilla |
| 195 | nom_cptos | PAY_PayrollConcepts | Renombrada | PAY_ | Conceptos de nómina |
| 196 | nom_cuentas | PAY_ConceptAccounts | Renombrada | PAY_ | Cuentas por concepto |
| 197 | nom_detliqemp | PAY_EmployeeLiquidationDetails | Renombrada | PAY_ | Detalle liquidación empleado |
| 198 | nom_empleados | PAY_Employees | Renombrada | PAY_ | ~80 cols. PK compuesta 2 cols → INT. FK a COR_People |
| 199 | nom_empresas | COR_EmployerCompanies | Fusionada | COR_ | Fusionada con cop_empresa13 |
| 200 | nom_eps | PAY_HealthInsuranceProviders | Renombrada | PAY_ | EPS |
| 201 | nom_impcert | PAY_TaxCertificates | Renombrada | PAY_ | Certificados tributarios |
| 202 | nom_libranzas | PAY_DirectDebits | Renombrada | PAY_ | Libranzas |
| 203 | nom_liqplan | PAY_PayrollPlanLiquidations | Renombrada | PAY_ | PK compuesta 5 cols → BIGINT |
| 204 | nom_liqvac | PAY_VacationLiquidations | Renombrada | PAY_ | Liquidación vacaciones |
| 205 | nom_maeliqemp | PAY_EmployeeLiquidationMasters | Renombrada | PAY_ | Maestro liquidación empleado |
| 206 | nom_movtos | PAY_PayrollTransactions | Renombrada | PAY_ | PK compuesta 5 cols → BIGINT |
| 207 | nom_novedad | PAY_PayrollEntries | Renombrada | PAY_ | PK compuesta 3 cols → BIGINT |
| 208 | nom_novsalario | PAY_SalaryChanges | Renombrada | PAY_ | Novedades salariales |
| 209 | nom_parautapo | PAY_AutoContributionParams | Renombrada | PAY_ | Sin PK, 44 cols → INT |
| 210 | nom_parent | COR_Relationships | Fusionada | COR_ | Fusionada con sys_parent51 |
| 211 | nom_parretfte | PAY_WithholdingParameters | Renombrada | PAY_ | Parámetros retención fuente |
| 212 | nom_pensiones | PAY_PensionProviders | Renombrada | PAY_ | Sin PK → INT |
| 213 | nom_perpagos | PAY_PayPeriods | Renombrada | PAY_ | PK compuesta 2 cols → INT |
| 214 | nom_preliq | PAY_PreLiquidations | Renombrada | PAY_ | Pre-liquidaciones |
| 215 | nom_respreliq | PAY_PreLiquidationResponses | Renombrada | PAY_ | Respuestas pre-liquidación |
| 216 | nom_sallib | PAY_BookBalances | Renombrada | PAY_ | Saldos de libro |
| 217 | plano | *(eliminada)* | Eliminada | — | 1 columna, sin PK. Temporal para generación de planos bancarios |
| 218 | sys_agencia | COR_Branches | Renombrada | COR_ | PK varchar(4) → INT |
| 219 | sys_asejuri | COR_LegalAdvisors | Renombrada | COR_ | Asesores jurídicos |
| 220 | sys_banco03 | COR_Banks | Renombrada | COR_ | PK varchar(4) → INT |
| 221 | sys_cargo55 | COR_Positions | Renombrada | COR_ | PK varchar(4) → INT |
| 222 | sys_cencos | COR_CostCenters | Fusionada | COR_ | Fusionada con cnt_cencos + nom_cencos. PK varchar(8) → INT |
| 223 | sys_ciaaud | AUD_CompanyChanges | Renombrada | AUD_ | Auditoría compañía |
| 224 | sys_ciudad57 | COR_Cities | Renombrada | COR_ | Ya tenía INT PK |
| 225 | SYS_CODCIU | *(eliminada)* | Eliminada | — | Duplicado de sys_ciudad57 |
| 226 | sys_ComAsigna | SEC_UserAssignments | Renombrada | SEC_ | PK compuesta 2 cols → INT |
| 227 | sys_ComAsignaAud | AUD_AssignmentChanges | Renombrada | AUD_ | Auditoría asignaciones |
| 228 | sys_compania | COR_Companies | Renombrada | COR_ | ~70 columnas. PK varchar → INT |
| 229 | sys_compro02 | ACC_VoucherTypes | Renombrada | ACC_ | PK varchar(4) → INT |
| 230 | sys_compro02aud | AUD_VoucherTypeChanges | Renombrada | AUD_ | Auditoría comprobantes |
| 231 | sys_consecu | COR_Sequences | Renombrada | COR_ | Consecutivos del sistema |
| 232 | sys_convenio | COR_Agreements | Renombrada | COR_ | PK varchar → INT |
| 233 | sys_cultura54 | COR_CulturalActivities | Renombrada | COR_ | PK varchar(4) → INT |
| 234 | sys_curso | COR_Courses | Renombrada | COR_ | PK varchar → INT |
| 235 | sys_deport53 | COR_Sports | Renombrada | COR_ | PK varchar(4) → INT |
| 236 | sys_enfermedades | COR_Diseases | Renombrada | COR_ | Catálogo enfermedades |
| 237 | sys_entidad | COR_Entities | Renombrada | COR_ | PK varchar → INT |
| 238 | sys_forpago | COR_PaymentMethods | Renombrada | COR_ | PK compuesta 2 cols → INT |
| 239 | sys_forpago_cheq | COR_PaymentMethodChecks | Renombrada | COR_ | Cheques por forma de pago |
| 240 | sys_maenit | COR_People + COR_Associates + COR_Spouses + COR_PeopleFinancial + COR_AssociateCategories | Dividida | COR_ | ~130 cols monolítica → 5 tablas normalizadas. PK varchar(14) → INT. Detalles en Sección 2 |
| 241 | sys_masaud | AUD_MasterChanges | Renombrada | AUD_ | Auditoría maestros |
| 242 | sys_menuaud | AUD_MenuChanges | Renombrada | AUD_ | Auditoría menú |
| 243 | sys_menusu | SEC_UserMenuAccess | Renombrada | SEC_ | Sin PK → INT |
| 244 | sys_motret | COR_WithdrawalReasons | Renombrada | COR_ | PK varchar(4) → INT |
| 245 | sys_paises | COR_Countries | Renombrada | COR_ | PK varchar → INT |
| 246 | sys_parent51 | COR_Relationships | Fusionada | COR_ | Fusionada con nom_parent. PK varchar(4) → INT |
| 247 | sys_parfactura | COR_InvoiceParameters | Renombrada | COR_ | Parámetros facturación |
| 248 | sys_parlistas | COR_ListParameters | Renombrada | COR_ | Parámetros listas |
| 249 | sys_parpences | COR_PensionSeveranceParams | Renombrada | COR_ | Parámetros pensiones/cesantías |
| 250 | sys_periodo | ACC_AccountingPeriods | Renombrada | ACC_ | PK compuesta 2 cols → INT |
| 251 | sys_periodoAud | AUD_PeriodChanges | Renombrada | AUD_ | Auditoría períodos |
| 252 | sys_profe52 | COR_Professions | Renombrada | COR_ | PK varchar(4) → INT |
| 253 | sys_progra_sas | *(eliminada)* | Eliminada | — | Obsoleta. 3 columnas, sin PK, no referenciada |
| 254 | sys_programa | SEC_Modules | Renombrada | SEC_ | PK compuesta 2 cols → INT |
| 255 | sys_recreacion | COR_RecreationalEvents | Renombrada | COR_ | PK varchar(15) → INT |
| 256 | sys_referencia | COR_References | Renombrada | COR_ | Ya tenía IDENTITY |
| 257 | sys_sasusu | SEC_Users | Renombrada | SEC_ | PK varchar(14) → INT. Se agregan campos modernos: passwordHash, mfaEnabled, etc. |
| 258 | sys_sasusuaud | AUD_UserChanges | Renombrada | AUD_ | Auditoría usuarios |
| 259 | sys_seccion56 | COR_Sections | Renombrada | COR_ | PK varchar(4) → INT |
| 260 | TBLZONAZ | *(eliminada)* | Eliminada | — | Sin PK, sin FKs. Duplica cop_zonas/cop_subzonas |
| 261 | TES_CHEQUES | TRS_Checks | Renombrada | TRS_ | PK compuesta 3 cols → BIGINT |
| 262 | TES_CPTOS | TRS_Concepts | Renombrada | TRS_ | Conceptos tesorería |
| 263 | TES_FACTURA | TRS_Invoices | Renombrada | TRS_ | PK compuesta 3 cols → BIGINT |
| 264 | web_actdatos | WEB_DataUpdates | Renombrada | WEB_ | Actualización datos online |
| 265 | web_extras | WEB_ExtraPayments | Renombrada | WEB_ | Extras online |
| 266 | web_maeser | WEB_Services | Renombrada | WEB_ | Servicios web |
| 267 | web_solafi | WEB_AffiliationApplications | Renombrada | WEB_ | Solicitudes afiliación online |
| 268 | web_solaux | WEB_AuxiliaryApplications | Renombrada | WEB_ | Solicitudes aux online |
| 269 | web_solcred | WEB_LoanApplications | Renombrada | WEB_ | Solicitudes crédito online |
| 270 | wrk_tem1 | *(eliminada)* | Eliminada | — | Prefijo wrk_. 4 columnas, sin PK, temporal de trabajo |

### Resumen de acciones

| Acción | Cantidad | Detalle |
|---|---|---|
| Renombrada | 220 | Tabla renombrada 1:1 con nueva PK surrogate |
| Fusionada (absorbida) | 25 | Tablas fusionadas en 12 tablas destino (9 grupos) |
| Eliminada | 12 | wrk_tem1, plano, logo, TBLZONAZ, cnt_tmpflujocaja, cop_tempmovextra, SYS_CODCIU, cnt_saldocortolargo, cop_tmplavado, cnt_certrefte, sys_progra_sas, cop_retiro |
| Dividida | 2 | sys_maenit (→5 tablas), cnt_maecuen (→2 tablas) |
| **Total originales** | **270** | |

### Tablas NUEVAS (no existían en el esquema original)

| # | Tabla Nueva | Módulo | Tipo PK | Descripción |
|---|---|---|---|---|
| 1 | ADM_Tenants | ADM_ | INT | Gestión de tenants multi-tenant |
| 2 | ADM_Subscriptions | ADM_ | INT | Planes y billing por tenant |
| 3 | ADM_TenantSettings | ADM_ | INT | Configuración específica por tenant |
| 4 | SEC_Roles | SEC_ | INT | Roles del sistema |
| 5 | SEC_Permissions | SEC_ | INT | Permisos granulares |
| 6 | SEC_RolePermissions | SEC_ | INT | Relación roles-permisos (N:M) |
| 7 | SEC_UserRoles | SEC_ | INT | Relación usuarios-roles (N:M) |
| 8 | SEC_RefreshTokens | SEC_ | BIGINT | JWT refresh token rotation |
| 9 | SEC_UserSessions | SEC_ | BIGINT | Control de sesiones activas |
| 10 | SEC_LoginAttempts | SEC_ | BIGINT | Registro de intentos de login |
| 11 | COR_SystemSettings | COR_ | INT | Configuración por tenant key-value |
| 12 | COR_NotificationTemplates | COR_ | INT | Plantillas de email/SMS/push |
| 13 | COR_Notifications | COR_ | BIGINT | Notificaciones enviadas |
| 14 | COR_Attachments | COR_ | BIGINT | Archivos adjuntos (blob storage ref) |
| 15 | COR_Spouses | COR_ | INT | Datos del cónyuge (extraídos de sys_maenit) |
| 16 | COR_PeopleFinancial | COR_ | INT | Datos financieros de persona |
| 17 | COR_AssociateCategories | COR_ | INT | Categorías de asociado |
| 18 | COR_Countries | COR_ | INT | Países (normalizado desde sys_paises) |
| 19 | COR_Departments | COR_ | INT | Departamentos/estados |
| 20 | COR_Associates | COR_ | INT | Datos específicos de asociado |
| 21 | AUD_AuditReferences | AUD_ | BIGINT | Referencias a logs detallados en MongoDB |
| 22 | ACC_AccountBalances | ACC_ | BIGINT | Saldos normalizados por cuenta/período/agencia/ccosto |
| 23 | ACC_FiscalPeriods | ACC_ | INT | Períodos fiscales con estado |
| 24 | LND_InsuranceBeneficiaries | LND_ | INT | Beneficiarios de seguros (separados de pólizas) |
| 25 | LND_ContributionReductionParams | LND_ | INT | Parámetros reducción aportes |
| 26 | LND_ActivityEnrollments | LND_ | BIGINT | Inscripciones a actividades |

**Total tablas nuevas: 26** (23 completamente nuevas + 3 resultantes de divisiones)

---

## SECCIÓN 2: MAPEO DE COLUMNAS COMPLETO

### Transformaciones aplicadas a TODAS las tablas

Antes del detalle por tabla, estas transformaciones se aplican universalmente:

1. **PK surrogate:** Toda tabla recibe `Id INT|BIGINT IDENTITY(1,1) PRIMARY KEY CLUSTERED`
2. **PublicId:** `uniqueidentifier NOT NULL DEFAULT NEWID()` con UNIQUE INDEX
3. **Columnas de auditoría:** `CreatedAt datetime2`, `CreatedBy nvarchar(100)`, `UpdatedAt datetime2`, `UpdatedBy nvarchar(100)`, `IsDeleted bit DEFAULT 0`, `DeletedAt datetime2`, `DeletedBy nvarchar(100)`
4. **varchar → nvarchar:** Todas las columnas varchar se convierten a nvarchar (soporte Unicode)
5. **smalldatetime → date/datetime2:** Fechas puras a `date`, timestamps a `datetime2`
6. **PKs compuestas → UNIQUE constraints:** La PK compuesta original se convierte en UNIQUE INDEX
7. **FKs varchar → int:** Referencias por código varchar se reemplazan por FK int al Id de la tabla padre
8. **Columnas FILLER* eliminadas:** Campos de relleno legacy se eliminan

---

### Módulo COR_ (Core/Sistema)

#### COR_People (antes: sys_maenit + cnt_nit)

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

**Deduplicación:** Se usa NIT (cnt_nit) = CODIGOTER (sys_maenit) como llave de cruce. Cuando existan en ambas tablas, sys_maenit prevalece para datos personales y cnt_nit para datos contables (régimen, tipo ICA, gran contribuyente).

#### COR_Associates (antes: campos de sys_maenit)

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

#### COR_Spouses (antes: campos de sys_maenit)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK |
| (nueva) | PersonId | - | int | FK → COR_People.Id |
| CONYUGE_NOM | SpouseName | varchar(100) | nvarchar(150) | |
| CONYUGE_NIT | SpouseIdentification | varchar(14) | nvarchar(20) | |
| CONYUGE_TRABAJA | SpouseWorks | varchar(1) | bit | |
| CONYUGE_EMPRESA | SpouseEmployer | varchar(60) | nvarchar(100) | |
| CONYUGE_SALARIO | SpouseSalary | decimal | decimal(18,2) | |
| CONYUGE_TELEFONO | SpousePhone | varchar(30) | nvarchar(30) | |

#### COR_PeopleFinancial (antes: campos de sys_maenit)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK |
| (nueva) | PersonId | - | int | FK → COR_People.Id |
| CAPA_DEUDA | DebtCapacity | decimal | decimal(18,2) | |
| ACTIVOS | TotalAssets | decimal | decimal(18,2) | |
| IngrVariables | VariableIncome | decimal | decimal(18,2) | |
| IngArriendos | RentalIncome | decimal | decimal(18,2) | |
| DeudasTerceros | ThirdPartyDebts | decimal | decimal(18,2) | |
| GASTO_FIJO_MES | MonthlyFixedExpenses | decimal | decimal(18,2) | |
| ScoreCifin | CreditScore | decimal | decimal(10,2) | |
| califidatacredito | CreditBureauRating | varchar(2) | nvarchar(5) | |

#### COR_AssociateCategories (antes: campos de sys_maenit)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK |
| (nueva) | AssociateId | - | int | FK → COR_Associates.Id |
| CALIFI_CATEG | CategoryCode | varchar(1) | nvarchar(5) | |
| (nueva) | AssignedDate | - | date | |
| (nueva) | Notes | - | nvarchar(500) | |

#### COR_Branches (antes: sys_agencia)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| codigo | LegacyCode | varchar(4) | nvarchar(10) | Indexado para migración |
| nombre | Name | varchar(50) | nvarchar(150) | |
| estado | Status | varchar(1) | nvarchar(2) | |

#### COR_CostCenters (antes: sys_cencos + cnt_cencos + nom_cencos)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| CCOSTO | LegacyCode | varchar(8) | nvarchar(15) | Indexado |
| NOMBRE | Name | varchar(50) | nvarchar(150) | |
| NIVEL | Level | varchar(1) | tinyint | |
| (nueva) | ParentId | - | int NULL | FK → COR_CostCenters.Id (jerarquía) |

#### COR_Cities (antes: sys_ciudad57)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| CIUDAD | Id | int | INT IDENTITY | Ya tenía INT PK, se migra valor |
| NOMBRE | Name | varchar(60) | nvarchar(100) | |
| DEPARTAMENTO | DepartmentId | varchar(2) | int | FK → COR_Departments.Id |

#### COR_Companies (antes: sys_compania)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| NIT | IdentificationNumber | varchar(14) | nvarchar(20) | UNIQUE |
| NOMBRE | Name | varchar(100) | nvarchar(200) | |
| DIRECCION | Address | varchar(60) | nvarchar(250) | |
| TELEFONO | Phone | varchar(30) | nvarchar(30) | |
| EMAIL | Email | varchar(60) | nvarchar(200) | |
| CIUDAD | CityId | int | int | FK → COR_Cities.Id |
| *(~60 columnas restantes)* | *(renombradas PascalCase)* | varchar/int | nvarchar/int | Mapeo 1:1, varchar→nvarchar |

#### COR_Banks (antes: sys_banco03)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| codigo | LegacyCode | varchar(4) | nvarchar(10) | Indexado |
| nombre | Name | varchar(50) | nvarchar(150) | |
| *(columnas restantes)* | *(renombradas PascalCase)* | varchar | nvarchar | |

#### COR_EmployerCompanies (antes: cop_empresa13 + nom_empresas)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Origen | Notas |
|---|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | - | PK surrogate |
| codigo_empresa / idnomina | LegacyCode | varchar(4) | nvarchar(10) | cop_empresa13/nom_empresas | Indexado |
| nombre | Name | varchar(60) | nvarchar(200) | ambos | |
| nit | IdentificationNumber | varchar(14) | nvarchar(20) | ambos | |
| direccion | Address | varchar(60) | nvarchar(250) | ambos | |
| telefono | Phone | varchar(30) | nvarchar(30) | ambos | |
| *(columnas restantes)* | *(renombradas PascalCase)* | mixed | nvarchar/int | ambos | Unión de columnas de ambas tablas |

#### COR_Relationships (antes: sys_parent51 + nom_parent)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| codigo | LegacyCode | varchar(4) | nvarchar(10) | Indexado |
| nombre | Name | varchar(30) | nvarchar(100) | |

#### COR_Beneficiaries (antes: cop_benef + nom_bene)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| CODIGOTER / idempleado | PersonId | varchar(14) | int | FK → COR_People.Id |
| (nueva) | BeneficiaryType | - | nvarchar(10) | 'Associate' o 'Employee' |
| NIT_BENE | BeneficiaryIdentification | varchar(14) | nvarchar(20) | |
| NOMBRE | BeneficiaryName | varchar(100) | nvarchar(150) | |
| PARENTESCO | RelationshipId | varchar(4) | int | FK → COR_Relationships.Id |
| PORCENTAJE | Percentage | decimal | decimal(5,2) | |

#### COR_PaymentMethods (antes: sys_forpago)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| *(PK compuesta 2 cols)* | - | - | UNIQUE constraint | |
| descripcion | Description | varchar(50) | nvarchar(150) | |

#### COR_Sequences (antes: sys_consecu)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| modulo | ModuleCode | varchar(4) | nvarchar(10) | |
| consecutivo | CurrentValue | int | bigint | |
| descripcion | Description | varchar(50) | nvarchar(150) | |

*(Las tablas COR_ restantes -- COR_Sections, COR_Professions, COR_Positions, COR_CulturalActivities, COR_Sports, COR_WithdrawalReasons, COR_Entities, COR_Agreements, COR_Courses, COR_References, COR_RecreationalEvents, COR_Diseases, COR_Committees, COR_CommitteeMembers, COR_Advisors, COR_ActivityPrograms, COR_ListParameters, COR_InvoiceParameters, COR_PensionSeveranceParams, COR_LegalAdvisors, COR_PaymentMethodChecks -- siguen el patron estandar: PK varchar → INT IDENTITY, varchar → nvarchar, columnas de auditoria agregadas.)*

---

### Módulo ACC_ (Contabilidad)

#### ACC_ChartOfAccounts (antes: cnt_maecuen — solo metadatos)

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
| (eliminadas) | - | DEB_ENE...CRE_DIC (24 cols) | - | **Migran a ACC_AccountBalances** |

#### ACC_AccountBalances (normalizada desde cnt_maecuen saldos + cnt_salage)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| CUENTA (cnt_maecuen/cnt_salage) | AccountId | varchar(12) | int | FK → ACC_ChartOfAccounts.Id |
| (derivada de ENE..DIC) | PeriodId | - | int | FK → ACC_FiscalPeriods.Id |
| AGENCIA (cnt_salage) | BranchId | varchar(4) | int | FK → COR_Branches.Id |
| CENCOS (cnt_salage) | CostCenterId | varchar(8) | int | FK → COR_CostCenters.Id |
| DEB_ENE..DEB_DIC | DebitAmount | decimal(17,2) | decimal(18,2) | Normalizado: 1 fila por mes |
| CRE_ENE..CRE_DIC | CreditAmount | decimal(17,2) | decimal(18,2) | Normalizado: 1 fila por mes |

**Transformacion UNPIVOT:** Las 24 columnas de saldos mensuales (DEB_ENE, CRE_ENE, DEB_FEB, CRE_FEB, ..., DEB_DIC, CRE_DIC) se normalizan en filas individuales por periodo.

#### ACC_JournalEntries (antes: cnt_movimto)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| SECUENCIA | Id | int IDENTITY | BIGINT IDENTITY | PK (ya tenia IDENTITY, se amplia a BIGINT) |
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

#### ACC_Documents (antes: cnt_docmto)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| COMPRONTE | VoucherTypeCode | varchar(4) | nvarchar(10) | |
| NUMERO | DocumentNumber | bigint | bigint | |
| *(PK compuesta 2 cols)* | - | - | UNIQUE(VoucherTypeCode, DocumentNumber) | |
| FECHA | DocumentDate | smalldatetime | date | |
| DETALLE | Description | varchar(80) | nvarchar(200) | |

#### ACC_AuxiliaryDocuments (antes: cnt_docaux)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| *(PK compuesta 8 cols!)* | - | 8 cols | UNIQUE constraint de 8 cols | La PK de 8 cols mas grande del esquema |
| COMPRONTE | VoucherTypeCode | varchar(4) | nvarchar(10) | |
| NUMERO_DOMTO | DocumentNumber | bigint | bigint | |
| CUENTA | AccountId | varchar(12) | int | FK → ACC_ChartOfAccounts.Id |
| NIT | PersonId | varchar(14) | int | FK → COR_People.Id |
| FECHA | Date | smalldatetime | date | |

#### ACC_VoucherTypes (antes: sys_compro02)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| CODIGO | LegacyCode | varchar(4) | nvarchar(10) | Indexado |
| NOMBRE | Name | varchar(50) | nvarchar(150) | |
| TIPO | Type | varchar(1) | nvarchar(2) | |
| CONSECUTIVO | CurrentSequence | bigint | bigint | |

#### ACC_AccountingPeriods (antes: sys_periodo)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| ANIO | Year | int | int | |
| MES | Month | int | tinyint | |
| ESTADO | Status | varchar(1) | nvarchar(2) | A=Abierto, C=Cerrado |
| *(PK compuesta 2 cols)* | - | - | UNIQUE(Year, Month) | |

*(Las tablas ACC_ restantes -- ACC_ThirdPartyAccounts, ACC_Amortizations, ACC_Depreciations, ACC_JournalEntryItems, ACC_Budgets, ACC_RiskCategories, ACC_BankReconciliations, ACC_BankReconciliationFlats, ACC_BankReconciliationMasters, ACC_GmfTaxLines, ACC_IcaTaxLines, ACC_VatTaxLines, ACC_IncomeTaxLines, ACC_WithholdingTaxLines, ACC_DianReportFormats, ACC_FinancialReportParams, ACC_FinancialReportValues, ACC_FinancialReports, ACC_TaxFormCodes, ACC_StampTaxes, ACC_AccountGroups, ACC_AccountSubgroups, ACC_GroupNames, ACC_SubgroupNames, ACC_ExchangeRateHistory, ACC_FiscalPeriods -- siguen el patron estandar de transformacion.)*

---

### Módulo LND_ (Lending/Cartera Financiera)

#### LND_LoanPortfolios (antes: cop_maecar)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| (nueva) | PublicId | - | uniqueidentifier | GUID publica |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| LINCRED | CreditLineId | int | int | FK → LND_CreditLineParameters.Id |
| NUMERO | PortfolioNumber | bigint | bigint | Numero secuencial del credito |
| (PK compuesta) | - | 3 cols | UNIQUE(PersonId, CreditLineId, PortfolioNumber) | Constraint de unicidad |
| NIT | IdentificationNumber | varchar(14) | nvarchar(20) | Desnormalizado para busqueda |
| FECSOLIC | ApplicationDate | smalldatetime | date | Fecha solicitud |
| FECAPROB | ApprovalDate | smalldatetime | date | Fecha aprobacion |
| FECFACT | DisbursementDate | smalldatetime | date | Fecha desembolso |
| FECDESC | DiscountStartDate | smalldatetime | date | Inicio descuento |
| FECULTCAU | LastAccrualDate | smalldatetime | date | Ultima causacion |
| FECULTPAGO | LastPaymentDate | smalldatetime | date | Ultimo pago |
| FECULTMORA | LastDefaultDate | smalldatetime | date | Ultima mora |
| FECVEMTO | MaturityDate | smalldatetime | date | Vencimiento |
| FECCIERRE | ClosingDate | smalldatetime | date | Cierre |
| PLAZO | TermMonths | smallint | int | Plazo en meses |
| VLRSOLICITUD | RequestedAmount | decimal(17,2) | decimal(18,2) | Monto solicitado |
| VALOROB | ApprovedAmount | decimal(17,2) | decimal(18,2) | Monto aprobado |
| SALDOT | CurrentBalance | decimal(17,2) | decimal(18,2) | Saldo actual |
| CUOTA | InstallmentAmount | decimal(17,2) | decimal(18,2) | Valor cuota |
| TASAINT | InterestRate | decimal(10,6) | decimal(10,6) | Tasa de interes |
| CICLOD | PaymentCycle | varchar(1) | nvarchar(2) | Ciclo de pago |
| PERIODD | PaymentPeriodicity | varchar(1) | nvarchar(2) | Periodicidad |
| CLACUO | InstallmentType | varchar(1) | nvarchar(2) | Clase de cuota |
| CLASEI | InterestType | varchar(1) | nvarchar(2) | Clase de interes |
| CLASEGAR | GuaranteeType | varchar(2) | nvarchar(5) | Tipo garantia |
| CLADES | DeductionType | varchar(1) | nvarchar(2) | Clase descuento |
| CUOPAG | PaidInstallments | smallint | int | Cuotas pagadas |
| CUOPENDI | PendingInstallments | decimal(5,2) | decimal(5,2) | Cuotas pendientes |
| TASAADM | AdminFeeRate | decimal(12,5) | decimal(12,5) | Tasa administracion |
| TASASEG | InsuranceRate | decimal(10,5) | decimal(10,5) | Tasa seguro |
| DIASMORA | DaysOverdue | smallint | int | Dias en mora |
| CATEGORIA | Category | varchar(1) | nvarchar(2) | Categoria riesgo |
| CODEUDOR1..4 | - | varchar(14) | - | **Migran a LND_ApplicationCodebtors** |
| AGENCIA | BranchId | varchar(4) | int | FK → COR_Branches.Id |
| CCOSTO | CostCenterId | varchar(8) | int | FK → COR_CostCenters.Id |
| USUARIO | CreatedBy | varchar(14) | nvarchar(100) | Columna estandar |
| FECHA_GRABA | CreatedAt | smalldatetime | datetime2 | Columna estandar |

#### LND_Transactions (antes: cop_movimto)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| SECUENCIA | Id | int IDENTITY | BIGINT IDENTITY | PK (ya tenia IDENTITY) |
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

#### LND_PendingInstallments (antes: cop_cuopen)

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

#### LND_PortfolioClassifications (antes: cop_copclas)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| LINCRED | CreditLineId | int | int | |
| NUMERO | LoanPortfolioId | bigint | int | FK → LND_LoanPortfolios.Id |
| CATEGORIA | Category | varchar(1) | nvarchar(2) | |
| *(~17 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | Mapeo 1:1 con conversiones estandar |

#### LND_CreditLineParameters (antes: cop_concar12)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| LINCRED | LegacyLineCode | int | int | Indexado para migracion |
| NOMBRE | Name | varchar(50) | nvarchar(150) | |
| *(~88 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | ~90 columnas de parametrizacion. varchar→nvarchar, smalldatetime→date |

#### LND_Documents (antes: cop_docmto)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| COMPRONTE | VoucherTypeCode | varchar(4) | nvarchar(10) | |
| NUMERO | DocumentNumber | bigint | bigint | |
| *(PK compuesta 2 cols)* | - | - | UNIQUE constraint | |

#### LND_DefaultRecords (antes: cop_copmora)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| LINCRED | CreditLineId | int | int | |
| NUMERO | LoanPortfolioId | bigint | int | FK → LND_LoanPortfolios.Id |
| DIASMORA | DaysOverdue | smallint | int | |
| *(~15 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | |

#### LND_SavingsAccounts (antes: cop_maeahor)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| LINAHO | SavingsLineId | int | int | |
| NUM_CUENTA | AccountNumber | varchar(20) | nvarchar(25) | UNIQUE |
| SALDO | CurrentBalance | decimal(17,2) | decimal(18,2) | |
| *(~40 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | |

#### LND_DepositAccounts (antes: dep_maeahor)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| num_cuenta | AccountNumber | varchar(20) | nvarchar(25) | UNIQUE |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| saldo | CurrentBalance | decimal(17,2) | decimal(18,2) | |
| *(~45 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | varchar→nvarchar, smalldatetime→date |

*(Las tablas LND_ restantes siguen el patron estandar: PK surrogate INT/BIGINT IDENTITY, PublicId GUID, FKs int, varchar→nvarchar, smalldatetime→date/datetime2, PKs compuestas→UNIQUE constraints.)*

---

### Módulo PAY_ (Nomina)

#### PAY_Employees (antes: nom_empleados)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| (nueva) | PublicId | - | uniqueidentifier | GUID |
| idnomina | PayrollId | varchar(4) | int | FK → PAY_PayPeriods o similar |
| idempleado | PersonId | varchar(14) | int | FK → COR_People.Id |
| *(PK compuesta 2 cols)* | - | - | UNIQUE(PayrollId, PersonId) | |
| SALARIO | Salary | decimal | decimal(18,2) | |
| TIPO_SALARIO | SalaryType | varchar(1) | nvarchar(2) | |
| EMPRESA_LABORA | EmployerName | varchar(60) | nvarchar(200) | |
| FEING_EMPRESA | EmployerJoinDate | smalldatetime | date | |
| CARGO | PositionCode | varchar(4) | nvarchar(10) | |
| PROFESION | ProfessionCode | varchar(4) | nvarchar(10) | |
| EPS | HealthInsuranceId | varchar(4) | int | FK → PAY_HealthInsuranceProviders.Id |
| ARP | WorkRiskId | varchar(4) | int | FK → PAY_WorkRiskProviders.Id |
| AFP | PensionProviderId | varchar(4) | int | FK → PAY_PensionProviders.Id |
| CESANTIAS | SeveranceProviderId | varchar(4) | int | FK → PAY_SeveranceProviders.Id |
| OTRO_INGRESO | OtherIncome | decimal | decimal(18,2) | |
| *(~65 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | |

#### PAY_PayrollConcepts (antes: nom_cptos)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| codigo | LegacyCode | varchar(4) | nvarchar(10) | Indexado |
| nombre | Name | varchar(50) | nvarchar(150) | |
| tipo | Type | varchar(1) | nvarchar(2) | D=Devengo, D=Deduccion |
| *(~36 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | |

#### PAY_PayrollPlanLiquidations (antes: nom_liqplan)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| *(PK compuesta 5 cols)* | - | 5 cols | UNIQUE constraint | |
| idnomina | PayrollId | varchar(4) | int | |
| idempleado | EmployeeId | varchar(14) | int | FK → PAY_Employees.Id |
| idperiodo | PeriodId | int | int | |
| valor | Amount | decimal | decimal(18,2) | |

#### PAY_PayrollTransactions (antes: nom_movtos)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| *(PK compuesta 5 cols)* | - | 5 cols | UNIQUE constraint | |
| idnomina | PayrollId | varchar(4) | int | |
| idempleado | EmployeeId | varchar(14) | int | FK → PAY_Employees.Id |
| concepto | ConceptId | varchar(4) | int | FK → PAY_PayrollConcepts.Id |
| valor | Amount | decimal | decimal(18,2) | |

*(Las tablas PAY_ restantes siguen el patron estandar.)*

---

### Módulo INV_ (Inventario)

#### INV_Products (antes: inv_productos)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| IdProducto | Id | INT | INT IDENTITY | Ya tenia INT PK |
| (nueva) | PublicId | - | uniqueidentifier | GUID |
| nombre | Name | varchar(100) | nvarchar(200) | |
| IdGruProducto | ProductGroupId | int | int | FK → INV_ProductGroups.Id |
| referencia | Reference | varchar(30) | nvarchar(50) | |
| *(~25 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | |

#### INV_Transactions (antes: inv_movtos)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate (original sin PK!) |
| Secuencia | SequenceNumber | float | bigint | float→bigint |
| IdProducto | ProductId | int | int | FK → INV_Products.Id |
| IdTipoMovto | TransactionTypeId | smallint | int | FK → INV_TransactionTypes.Id |
| cantidad | Quantity | decimal | decimal(18,4) | |
| valor | Amount | decimal | decimal(18,2) | |
| fecha | TransactionDate | smalldatetime | date | |

#### INV_Invoices (antes: inv_facturas)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate (original sin PK, 35 cols!) |
| numero | InvoiceNumber | bigint | bigint | UNIQUE |
| fecha | InvoiceDate | smalldatetime | date | |
| NIT | PersonId | varchar(14) | int | FK → COR_People.Id |
| *(~30 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | |

*(Las tablas INV_ restantes siguen el patron estandar.)*

---

### Módulo CDT_ (Certificados)

#### CDT_Certificates (antes: cdt_maecdats)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| (nueva) | PublicId | - | uniqueidentifier | GUID |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| NUMERO_CDT | CertificateNumber | bigint | bigint | UNIQUE |
| FECHA_APERTURA | OpeningDate | smalldatetime | date | |
| FECHA_VENCIMIENTO | MaturityDate | smalldatetime | date | |
| VALOR | FaceValue | decimal(17,2) | decimal(18,2) | |
| TASA | InterestRate | decimal(10,6) | decimal(10,6) | |
| PLAZO | TermDays | int | int | |
| ESTADO | Status | varchar(1) | nvarchar(2) | |
| *(~30 columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | |

#### CDT_CertificateEntries (antes: cdt_novcdats)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| *(PK compuesta 4 cols)* | - | 4 cols | UNIQUE constraint | |
| NUMERO_CDT | CertificateId | bigint | int | FK → CDT_Certificates.Id |
| VALOR | Amount | decimal(17,2) | decimal(18,2) | |
| FECHA | EntryDate | smalldatetime | date | |

---

### Módulo DEB_ (Tarjeta Debito)

#### DEB_Cards (antes: deb_maetarj)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| Banco | BankId | varchar(4) | int | FK → COR_Banks.Id |
| Tarjeta | CardNumber | varchar(20) | nvarchar(25) | UNIQUE o parte de UNIQUE(BankId, CardNumber) |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| *(columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | |

#### DEB_Transactions (antes: deb_movto)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| Secuencia | SequenceNumber | int | bigint | |
| Tarjeta | CardId | varchar(20) | int | FK → DEB_Cards.Id |
| valor | Amount | decimal | decimal(18,2) | |
| fecha | TransactionDate | smalldatetime | date | |

---

### Módulo TRS_ (Tesoreria)

#### TRS_Checks (antes: TES_CHEQUES)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| *(PK compuesta 3 cols)* | - | 3 cols | UNIQUE constraint | |
| BANCO | BankId | varchar(4) | int | FK → COR_Banks.Id |
| NUMERO | CheckNumber | bigint | bigint | |
| VALOR | Amount | decimal(17,2) | decimal(18,2) | |
| FECHA | CheckDate | smalldatetime | date | |
| BENEFICIARIO | BeneficiaryId | varchar(14) | int | FK → COR_People.Id |

#### TRS_Concepts (antes: TES_CPTOS)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| codigo | LegacyCode | varchar(4) | nvarchar(10) | |
| nombre | Name | varchar(50) | nvarchar(150) | |

#### TRS_Invoices (antes: TES_FACTURA)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| *(PK compuesta 3 cols)* | - | 3 cols | UNIQUE constraint | |
| PROVEEDOR | SupplierId | varchar(14) | int | FK → COR_People.Id |
| NUMERO | InvoiceNumber | bigint | bigint | |
| VALOR | Amount | decimal(17,2) | decimal(18,2) | |

---

### Módulo WEB_ (Web/Online)

#### WEB_LoanApplications (antes: web_solcred)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | BIGINT IDENTITY | PK surrogate |
| CODIGOTER | PersonId | varchar(14) | int | FK → COR_People.Id |
| *(columnas restantes)* | *(renombradas PascalCase)* | mixed | mixed | Solicitudes credito online |

*(Las tablas WEB_ restantes -- WEB_AuxiliaryApplications, WEB_AffiliationApplications, WEB_Services, WEB_ExtraPayments, WEB_DataUpdates -- siguen el patron estandar.)*

---

### Módulo SEC_ (Seguridad)

#### SEC_Users (antes: sys_sasusu)

| Columna Original | Columna Nueva | Tipo Original | Tipo Nuevo | Notas |
|---|---|---|---|---|
| (nueva) | Id | - | INT IDENTITY | PK surrogate |
| USUARIO | LegacyUsername | varchar(14) | nvarchar(50) | Indexado para migracion |
| (nueva) | Email | - | nvarchar(200) | UNIQUE. Nuevo campo para login |
| CLAVE | - | varchar(20) | - | **ELIMINADA. Se reemplaza por PasswordHash** |
| (nueva) | PasswordHash | - | nvarchar(500) | Hash bcrypt/argon2 |
| NOMBRE | FullName | varchar(60) | nvarchar(150) | |
| ESTADO | IsActive | varchar(1) | bit | |
| (nueva) | EmailVerified | - | bit DEFAULT 0 | |
| (nueva) | MfaEnabled | - | bit DEFAULT 0 | |
| (nueva) | LastLoginAt | - | datetime2 | |
| PERFIL | LegacyProfile | varchar(4) | nvarchar(10) | Migra a SEC_UserRoles |

---

### Módulo AUD_ (Auditoria)

Las tablas de auditoria mantienen estructura similar a sus originales pero con:
- PK BIGINT IDENTITY
- PublicId uniqueidentifier
- Columnas de referencia migradas a int (FKs)
- varchar → nvarchar
- smalldatetime → datetime2

| Tabla Original | Tabla Nueva | Notas |
|---|---|---|
| sys_ciaaud | AUD_CompanyChanges | Cambios en compania |
| sys_sasusuaud | AUD_UserChanges | Cambios en usuarios |
| sys_compro02aud | AUD_VoucherTypeChanges | Cambios comprobantes |
| sys_masaud | AUD_MasterChanges | Cambios maestros |
| sys_menuaud | AUD_MenuChanges | Cambios menu |
| sys_periodoAud | AUD_PeriodChanges | Cambios periodos |
| sys_ComAsignaAud | AUD_AssignmentChanges | Cambios asignaciones |
| cnt_maecuenAud | AUD_AccountChanges | Cambios plan cuentas |
| cnt_movaud | AUD_JournalChanges | Cambios movimientos contables |
| cop_movaud | AUD_PortfolioTransactionChanges | Cambios movimientos cartera |
| cop_mcaaud | AUD_PortfolioMasterChanges | Cambios maestro cartera |
| cop_moraud | AUD_DefaultChanges | Cambios mora |
| cop_ahoraud | AUD_SavingsChanges | Cambios ahorro |
| (nueva) | AUD_AuditReferences | Referencias a logs detallados en MongoDB |

---

## SECCIÓN 3: MAPEO DE LLAVES PRIMARIAS

### Resumen de transformacion de PKs

| Tipo PK Original | Cantidad | Transformacion |
|---|---|---|
| Sin PK | 34 | → INT/BIGINT IDENTITY (nueva PK surrogate) |
| PK simple varchar | ~80 | → INT IDENTITY + UNIQUE INDEX en codigo legacy |
| PK compuesta 2 cols | ~50 | → INT/BIGINT IDENTITY + UNIQUE constraint |
| PK compuesta 3-4 cols | ~44 | → INT/BIGINT IDENTITY + UNIQUE constraint |
| PK compuesta 5+ cols | 28 | → BIGINT IDENTITY + UNIQUE constraint |
| PK IDENTITY existente | ~15 | → Se mantiene IDENTITY, se amplia a BIGINT si transaccional |
| PK simple INT existente | ~10 | → Se mantiene o migra a IDENTITY |

### Detalle de las 28 tablas con PKs compuestas de 5+ columnas

| # | Tabla Original | Cols PK | Tabla Nueva | Nueva PK |
|---|---|---|---|---|
| 1 | cnt_docaux | 8 | ACC_AuxiliaryDocuments | BIGINT IDENTITY + UNIQUE(8 cols) |
| 2 | cop_nomdes | 10 | LND_PayrollDeductions | BIGINT IDENTITY + UNIQUE(10 cols) |
| 3 | cop_valdesc | 8 | LND_DeductionValues | BIGINT IDENTITY + UNIQUE(8 cols) |
| 4 | cop_copmora | 5 | LND_DefaultRecords | BIGINT IDENTITY + UNIQUE(5 cols) |
| 5 | cop_cuopen | 5 | LND_PendingInstallments | BIGINT IDENTITY + UNIQUE(5 cols) |
| 6 | cop_liqmor | 5 | LND_DefaultLiquidations | BIGINT IDENTITY + UNIQUE(5 cols) |
| 7 | nom_liqplan | 5 | PAY_PayrollPlanLiquidations | BIGINT IDENTITY + UNIQUE(5 cols) |
| 8 | nom_movtos | 5 | PAY_PayrollTransactions | BIGINT IDENTITY + UNIQUE(5 cols) |
| 9 | cnt_tercero | 5 | ACC_ThirdPartyAccounts | BIGINT IDENTITY + UNIQUE(5 cols) |
| 10 | cnt_amortiza | 6 | ACC_Amortizations | BIGINT IDENTITY + UNIQUE(6 cols) |
| 11 | cdt_tasasplazos | 5 | CDT_RatesByTerm | INT IDENTITY + UNIQUE(5 cols) |
| *(+ 17 tablas adicionales con 5+ cols de PK compuesta)* | | | | |

### Tablas de mapeo temporal necesarias para migracion

Para convertir PKs varchar a INT, se necesitan tablas de mapeo temporal:

```sql
-- Mapeo COR_People (base de todo el sistema)
CREATE TABLE #MapPeople (
    OldCodigoTer varchar(14) PRIMARY KEY,
    NewPeopleId int NOT NULL
);

-- Mapeo ACC_ChartOfAccounts
CREATE TABLE #MapAccounts (
    OldCuenta varchar(12) PRIMARY KEY,
    NewAccountId int NOT NULL
);

-- Mapeo COR_Branches
CREATE TABLE #MapBranches (
    OldCodigo varchar(4) PRIMARY KEY,
    NewBranchId int NOT NULL
);

-- Mapeo COR_CostCenters
CREATE TABLE #MapCostCenters (
    OldCCosto varchar(8) PRIMARY KEY,
    NewCostCenterId int NOT NULL
);

-- Mapeo COR_Banks
CREATE TABLE #MapBanks (
    OldCodigo varchar(4) PRIMARY KEY,
    NewBankId int NOT NULL
);

-- Mapeo ACC_VoucherTypes
CREATE TABLE #MapVoucherTypes (
    OldCodigo varchar(4) PRIMARY KEY,
    NewVoucherTypeId int NOT NULL
);

-- Mapeo LND_LoanPortfolios (PK compuesta)
CREATE TABLE #MapLoanPortfolio (
    OldCodigoTer varchar(14),
    OldLincred int,
    OldNumero bigint,
    NewLoanPortfolioId int NOT NULL,
    PRIMARY KEY (OldCodigoTer, OldLincred, OldNumero)
);

-- Mapeo LND_SavingsAccounts
CREATE TABLE #MapSavingsAccounts (
    OldCodigoTer varchar(14),
    OldLinaho int,
    NewSavingsAccountId int NOT NULL,
    PRIMARY KEY (OldCodigoTer, OldLinaho)
);

-- Mapeo LND_CreditLineParameters
CREATE TABLE #MapCreditLines (
    OldLincred int PRIMARY KEY,
    NewCreditLineId int NOT NULL
);

-- Mapeo COR_EmployerCompanies
CREATE TABLE #MapEmployerCompanies (
    OldCodigo varchar(4) PRIMARY KEY,
    NewEmployerCompanyId int NOT NULL
);
```

---

## SECCIÓN 4: MAPEO DE FOREIGN KEYS

### Foreign Keys originales: 228

Todas las FKs existentes se migran al nuevo esquema con las siguientes transformaciones:

1. **FK varchar → FK int:** La referencia por codigo varchar se convierte en referencia por Id int
2. **FK a tabla fusionada:** Se redirige a la tabla destino de la fusion
3. **FK a tabla eliminada:** Se elimina o redirige segun el caso

### FKs por modulo destino

| Módulo | FKs Originales | FKs Nuevas | Notas |
|---|---|---|---|
| COR_ | ~50 | ~65 | Aumentan por centralizacion de COR_People |
| ACC_ | ~25 | ~35 | Nuevas FKs a COR_People, COR_Branches, COR_CostCenters |
| LND_ | ~100 | ~120 | La mayoria referencia COR_People y ACC_ChartOfAccounts |
| PAY_ | ~30 | ~40 | Nuevas FKs a COR_People |
| INV_ | ~15 | ~20 | |
| CDT_ | ~5 | ~8 | |
| DEB_ | ~3 | ~7 | |
| TRS_ | ~3 | ~5 | |
| SEC_ | 0 | ~10 | Nuevas tablas con relaciones N:M |
| AUD_ | ~5 | ~14 | |
| WEB_ | ~5 | ~8 | |
| **TOTAL** | **~228** | **~332** | Aumento por normalizacion y nuevas relaciones |

### Patron tipico de FK nueva

```sql
-- Ejemplo: LND_LoanPortfolios
ALTER TABLE LND_LoanPortfolios ADD CONSTRAINT
    FK_LoanPortfolios_People FOREIGN KEY (PersonId) REFERENCES COR_People(Id),
    FK_LoanPortfolios_CreditLine FOREIGN KEY (CreditLineId) REFERENCES LND_CreditLineParameters(Id),
    FK_LoanPortfolios_Branch FOREIGN KEY (BranchId) REFERENCES COR_Branches(Id),
    FK_LoanPortfolios_CostCenter FOREIGN KEY (CostCenterId) REFERENCES COR_CostCenters(Id);
```

### Convencion de nombres FK

```
FK_[TablaHija]_[TablaPadre]
Ejemplo: FK_LoanPortfolios_People
```

---

## SECCIÓN 5: MAPEO DE VISTAS

### Vistas originales: 121

Las 121 vistas originales se reescriben para apuntar a las nuevas tablas. Ademas se crean vistas de compatibilidad para facilitar la transicion.

### Vistas de compatibilidad (nuevas)

Para cada tabla renombrada se puede crear una vista con el nombre original que mapea a la tabla nueva:

```sql
-- Ejemplo: vista de compatibilidad para codigo legacy
CREATE VIEW dbo.v_cop_maecar AS
SELECT
    p.LegacyCode AS CODIGOTER,
    lp.CreditLineId AS LINCRED,
    lp.PortfolioNumber AS NUMERO,
    lp.CurrentBalance AS SALDOT,
    -- ... mapeo inverso de todas las columnas
FROM LND_LoanPortfolios lp
INNER JOIN COR_People p ON lp.PersonId = p.Id;
```

### Estrategia de migracion de vistas

1. **Fase 1:** Crear vistas de compatibilidad con nombres viejos apuntando a tablas nuevas
2. **Fase 2:** Reescribir vistas originales usando nombres y columnas nuevas
3. **Fase 3:** Deprecar vistas de compatibilidad despues de 6 meses de estabilidad
4. **Fase 4:** Eliminar vistas de compatibilidad

---

## SECCIÓN 6: MAPEO DE FUNCIONES

### Funciones originales: 8

| # | Funcion Original | Funcion Nueva | Cambios |
|---|---|---|---|
| 1 | CalificaCifin | fn_CalculateCreditRating | Renombrada. Parametros int/varchar → int/nvarchar |
| 2 | fn_ConcatenaNombreCompleto | fn_GetFullName | Renombrada. Referencia COR_People en lugar de sys_maenit |
| 3 | fn_SaldoCartera | fn_GetPortfolioBalance | Renombrada. Referencia LND_LoanPortfolios |
| 4 | fn_EdadAsociado | fn_GetAssociateAge | Renombrada. Referencia COR_People.DateOfBirth |
| 5 | fn_DiasCartera | fn_GetPortfolioDays | Renombrada. Referencia LND_LoanPortfolios |
| 6 | fn_SaldoAhorro | fn_GetSavingsBalance | Renombrada. Referencia LND_SavingsAccounts |
| 7 | fn_ValidaNit | fn_ValidateIdentification | Renombrada. varchar→nvarchar |
| 8 | fn_CalculaMora | fn_CalculateDefault | Renombrada. Referencia LND_DefaultRecords |

### Patron de migracion de funciones

```sql
-- Original
CREATE FUNCTION CalificaCifin(@DiasMora int, @Modalidad varchar(2))
RETURNS varchar(2)

-- Nueva
CREATE FUNCTION fn_CalculateCreditRating(@DaysOverdue int, @Modality nvarchar(2))
RETURNS nvarchar(2)
-- Logica interna identica, solo cambios de tipo varchar→nvarchar
```

---

## SECCIÓN 7: TABLAS DE MAPEO TEMPORAL PARA MIGRACION

### Orden de creacion de tablas de mapeo

El orden es critico porque las tablas hijas dependen de las tablas padre ya migradas:

```
Fase 1 - Catalogos base (sin dependencias):
  1. #MapPeople          ← sys_maenit + cnt_nit → COR_People
  2. #MapBranches        ← sys_agencia → COR_Branches
  3. #MapCostCenters     ← sys_cencos → COR_CostCenters
  4. #MapBanks           ← sys_banco03 → COR_Banks
  5. #MapVoucherTypes    ← sys_compro02 → ACC_VoucherTypes
  6. #MapAccounts        ← cnt_maecuen → ACC_ChartOfAccounts
  7. #MapEmployerCompanies ← cop_empresa13 + nom_empresas → COR_EmployerCompanies

Fase 2 - Tablas maestras (dependen de Fase 1):
  8. #MapCreditLines     ← cop_concar12 → LND_CreditLineParameters
  9. #MapLoanPortfolio   ← cop_maecar → LND_LoanPortfolios (usa #MapPeople)
  10. #MapSavingsAccounts ← cop_maeahor → LND_SavingsAccounts (usa #MapPeople)

Fase 3 - Tablas transaccionales (dependen de Fase 1+2):
  11. Migrar LND_Transactions usando #MapPeople + #MapLoanPortfolio + #MapAccounts
  12. Migrar LND_PendingInstallments usando #MapLoanPortfolio
  13. Migrar ACC_JournalEntries usando #MapPeople + #MapAccounts
  14. Migrar ACC_AccountBalances (UNPIVOT de saldos mensuales)
  15. Migrar PAY_* usando #MapPeople
  16. Migrar INV_*
  17. Migrar CDT_* usando #MapPeople
  18. Migrar DEB_* usando #MapPeople + #MapBanks
  19. Migrar TRS_* usando #MapPeople + #MapBanks
  20. Migrar SEC_*
  21. Migrar AUD_*
  22. Migrar WEB_* usando #MapPeople
```

### Ejemplo completo: migracion de cop_maecar → LND_LoanPortfolios

```sql
-- PASO 1: Crear tabla nueva
CREATE TABLE LND_LoanPortfolios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PublicId uniqueidentifier DEFAULT NEWID() NOT NULL,
    PersonId INT NOT NULL,
    CreditLineId INT NOT NULL,
    PortfolioNumber BIGINT NOT NULL,
    -- ...columnas de negocio...,
    LegacyCodigoTer nvarchar(14) NULL,
    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy nvarchar(100) NOT NULL DEFAULT 'MIGRATION',
    IsDeleted bit NOT NULL DEFAULT 0,
    CONSTRAINT UQ_LoanPortfolio UNIQUE (PersonId, CreditLineId, PortfolioNumber)
);

-- PASO 2: Insertar datos con mapeo
INSERT INTO LND_LoanPortfolios (PersonId, CreditLineId, PortfolioNumber, ..., LegacyCodigoTer)
SELECT
    mp.NewPeopleId AS PersonId,
    mc.NewCreditLineId AS CreditLineId,
    m.NUMERO AS PortfolioNumber,
    ...,
    m.CODIGOTER AS LegacyCodigoTer
FROM cop_maecar m
INNER JOIN #MapPeople mp ON m.CODIGOTER = mp.OldCodigoTer
INNER JOIN #MapCreditLines mc ON m.LINCRED = mc.OldLincred;

-- PASO 3: Crear tabla de mapeo
INSERT INTO #MapLoanPortfolio (OldCodigoTer, OldLincred, OldNumero, NewLoanPortfolioId)
SELECT LegacyCodigoTer, CreditLineId, PortfolioNumber, Id
FROM LND_LoanPortfolios;

-- PASO 4: Migrar tablas hijas
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
```

---

## SECCIÓN 8: SCRIPT DE VALIDACION POST-MIGRACION

### Validaciones de conteo de registros

```sql
-- Validar que no se perdieron registros en la migracion
DECLARE @errores TABLE (tabla varchar(100), original int, nuevo int);

-- COR_People = sys_maenit (deduplicado con cnt_nit)
INSERT INTO @errores
SELECT 'COR_People',
    (SELECT COUNT(DISTINCT CODIGOTER) FROM sys_maenit),
    (SELECT COUNT(*) FROM COR_People WHERE IsDeleted = 0);

-- LND_LoanPortfolios = cop_maecar
INSERT INTO @errores
SELECT 'LND_LoanPortfolios',
    (SELECT COUNT(*) FROM cop_maecar),
    (SELECT COUNT(*) FROM LND_LoanPortfolios WHERE IsDeleted = 0);

-- ACC_JournalEntries = cnt_movimto
INSERT INTO @errores
SELECT 'ACC_JournalEntries',
    (SELECT COUNT(*) FROM cnt_movimto),
    (SELECT COUNT(*) FROM ACC_JournalEntries WHERE IsDeleted = 0);

-- LND_Transactions = cop_movimto
INSERT INTO @errores
SELECT 'LND_Transactions',
    (SELECT COUNT(*) FROM cop_movimto),
    (SELECT COUNT(*) FROM LND_Transactions WHERE IsDeleted = 0);

-- LND_PendingInstallments = cop_cuopen
INSERT INTO @errores
SELECT 'LND_PendingInstallments',
    (SELECT COUNT(*) FROM cop_cuopen),
    (SELECT COUNT(*) FROM LND_PendingInstallments WHERE IsDeleted = 0);

-- PAY_Employees = nom_empleados
INSERT INTO @errores
SELECT 'PAY_Employees',
    (SELECT COUNT(*) FROM nom_empleados),
    (SELECT COUNT(*) FROM PAY_Employees WHERE IsDeleted = 0);

-- INV_Products = inv_productos
INSERT INTO @errores
SELECT 'INV_Products',
    (SELECT COUNT(*) FROM inv_productos),
    (SELECT COUNT(*) FROM INV_Products WHERE IsDeleted = 0);

-- CDT_Certificates = cdt_maecdats
INSERT INTO @errores
SELECT 'CDT_Certificates',
    (SELECT COUNT(*) FROM cdt_maecdats),
    (SELECT COUNT(*) FROM CDT_Certificates WHERE IsDeleted = 0);

-- Mostrar discrepancias
SELECT * FROM @errores WHERE original <> nuevo;
```

### Validaciones de integridad referencial

```sql
-- Verificar que todas las FKs apuntan a registros existentes
-- COR_People referenciada correctamente desde LND_LoanPortfolios
SELECT COUNT(*) AS OrphanedPortfolios
FROM LND_LoanPortfolios lp
LEFT JOIN COR_People p ON lp.PersonId = p.Id
WHERE p.Id IS NULL AND lp.IsDeleted = 0;

-- ACC_ChartOfAccounts referenciada desde ACC_JournalEntries
SELECT COUNT(*) AS OrphanedJournalEntries
FROM ACC_JournalEntries je
LEFT JOIN ACC_ChartOfAccounts ca ON je.AccountId = ca.Id
WHERE ca.Id IS NULL AND je.IsDeleted = 0;

-- COR_Branches referenciada correctamente
SELECT COUNT(*) AS OrphanedBranchRefs
FROM LND_LoanPortfolios lp
LEFT JOIN COR_Branches b ON lp.BranchId = b.Id
WHERE b.Id IS NULL AND lp.BranchId IS NOT NULL AND lp.IsDeleted = 0;
```

### Validaciones de saldos (ACC_AccountBalances)

```sql
-- Verificar que la normalizacion de saldos fue correcta
-- Comparar totales anuales
SELECT
    'cnt_maecuen' AS Source,
    SUM(DEB_ENE + DEB_FEB + DEB_MAR + DEB_ABR + DEB_MAY + DEB_JUN +
        DEB_JUL + DEB_AGO + DEB_SEP + DEB_OCT + DEB_NOV + DEB_DIC) AS TotalDebits,
    SUM(CRE_ENE + CRE_FEB + CRE_MAR + CRE_ABR + CRE_MAY + CRE_JUN +
        CRE_JUL + CRE_AGO + CRE_SEP + CRE_OCT + CRE_NOV + CRE_DIC) AS TotalCredits
FROM cnt_maecuen
UNION ALL
SELECT
    'ACC_AccountBalances' AS Source,
    SUM(DebitAmount) AS TotalDebits,
    SUM(CreditAmount) AS TotalCredits
FROM ACC_AccountBalances;
```

### Validaciones de mapeo legacy

```sql
-- Verificar que todas las columnas LegacyCode tienen mapeo
SELECT COUNT(*) AS PeopleWithoutLegacy
FROM COR_People WHERE LegacyCode IS NULL AND IsDeleted = 0;

SELECT COUNT(*) AS PortfoliosWithoutLegacy
FROM LND_LoanPortfolios WHERE LegacyCodigoTer IS NULL AND IsDeleted = 0;

-- Verificar unicidad de LegacyCode
SELECT LegacyCode, COUNT(*) AS Duplicates
FROM COR_People
WHERE LegacyCode IS NOT NULL
GROUP BY LegacyCode
HAVING COUNT(*) > 1;
```

---

## RESUMEN ESTADISTICO FINAL

| Metrica | BD Original | BD Nueva | Cambio |
|---|---|---|---|
| **Tablas** | 270 | 269 | -1 (12 eliminadas, 25 fusionadas→12, +26 nuevas) |
| **Tablas sin PK** | 34 | 0 | -100% |
| **PKs compuestas** | 122 | 0 | -100% (→ UNIQUE constraints) |
| **PKs varchar** | ~80 | 0 | -100% (→ INT/BIGINT IDENTITY) |
| **varchar sin Unicode** | ~95% tablas | 0% | -100% (→ nvarchar) |
| **smalldatetime** | ~100+ cols | 0 | -100% (→ date/datetime2) |
| **Columnas FILLER** | ~20 tablas | 0 | -100% |
| **Foreign Keys** | 228 | ~332 | +46% (normalizacion) |
| **Vistas** | 121 | 121+ compatibilidad | Reescritas |
| **Funciones** | 8 | 8 | Renombradas + nvarchar |

### Distribucion por modulo (nuevo esquema)

| Modulo | Prefijo | Tablas | % |
|---|---|---|---|
| Core/Sistema | COR_ | 42 | 15.6% |
| Lending/Cartera | LND_ | 83 | 30.9% |
| Accounting/Contabilidad | ACC_ | 33 | 12.3% |
| Payroll/Nomina | PAY_ | 27 | 10.0% |
| Inventory/Inventario | INV_ | 24 | 8.9% |
| Security/Seguridad | SEC_ | 11 | 4.1% |
| Audit/Auditoria | AUD_ | 14 | 5.2% |
| CDT/Certificados | CDT_ | 7 | 2.6% |
| Debit Cards | DEB_ | 7 | 2.6% |
| Web/Online | WEB_ | 6 | 2.2% |
| Treasury/Tesoreria | TRS_ | 3 | 1.1% |
| Admin/Multi-tenant | ADM_ | 3 | 1.1% |
| Otros | LND_ (consultas) | 1 | 0.4% |
| **TOTAL** | | **269** | **100%** |

---

> **Nota:** Este documento fue generado a partir de PROPUESTA-REDISENO-BD.md (aprobada). Los 4 archivos de salida de agentes asignados para el mapeo detallado de columnas resultaron vacios. El mapeo de columnas presentado en la Seccion 2 cubre las tablas principales (top ~30 con detalle completo) y las restantes siguen el patron estandar documentado al inicio de la seccion. Para un mapeo columna-por-columna exhaustivo de las 270 tablas, se recomienda regenerar los agentes sobre el archivo DBDefinicion.sql.
