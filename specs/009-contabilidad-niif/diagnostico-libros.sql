-- Diagnóstico de sólo lectura previo a la migración ContabilidadNiif (feature 009).
-- PostgreSQL; esquema dbo. Ejecutar en CADA base de cooperativa antes y después de migrar.
-- Si «documentos» o «movimientos» no es cero, la migración se niega: no desplegar; decidir con el dueño.
select 'documentos' as chequeo, count(*)::text as detalle from dbo."ACC_Documents"
union all
select 'movimientos', count(*)::text from dbo."ACC_JournalEntries"
union all
select 'cuentas (con legado)', count(*)::text || ' (' || count(*) filter (where "LegacyCode" is not null)::text || ')' from dbo."ACC_ChartOfAccounts"
union all
select 'tipos de comprobante', string_agg("Code", ', ' order by "Code") from dbo."ACC_VoucherTypes" where not "IsDeleted"
union all
select 'corridas de nómina con comprobante', count(*)::text from dbo."PAY_PayrollRuns" where "AccountingDocumentId" is not null
union all
select 'cuentas por concepto de nómina', count(*)::text from dbo."PAY_ConceptDefinitionAccounts" where not "IsDeleted"
union all
select 'parametrizaciones por código sin cuenta',
  (select count(*) from dbo."LND_CreditLineParameters" p where not p."IsDeleted" and p."AccountCode" is not null
     and not exists (select 1 from dbo."ACC_ChartOfAccounts" c where c."AccountCode" = p."AccountCode"))::text
  || ' cartera, ' ||
  (select count(*) from dbo."INV_ProductAccounts" p where not p."IsDeleted" and p."NetAccountCode" is not null
     and not exists (select 1 from dbo."ACC_ChartOfAccounts" c where c."AccountCode" = p."NetAccountCode"))::text
  || ' inventario';
