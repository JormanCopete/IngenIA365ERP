-- Diagnóstico previo a ReconciliarBanderasDerivadasDePersona (sólo lectura, PostgreSQL, esquema dbo).
-- Corrido en PDN el 2026-09-13: ingenia365erp 0 personas; cooflopal 2 personas, 0 desalineadas, 0 eliminadas.
-- Uso: k3s kubectl -n erp-pdn exec -i erp-db-1 -c postgres -- psql -U postgres -d <base> -At < diagnostico-banderas.sql
select 'IsSalesperson sin ficha' as caso, count(*) from dbo."COR_People" p where p."IsSalesperson" and not p."IsDeleted"
   and not exists (select 1 from dbo."INV_Salespeople" s where s."PersonId"=p."Id" and not s."IsDeleted")
union all
select 'ficha vendedor sin bandera', count(*) from dbo."COR_People" p where not p."IsSalesperson" and not p."IsDeleted"
   and exists (select 1 from dbo."INV_Salespeople" s where s."PersonId"=p."Id" and not s."IsDeleted")
union all
select 'IsEmployee sin empleado vivo', count(*) from dbo."COR_People" p where p."IsEmployee" and not p."IsDeleted"
   and not exists (select 1 from dbo."PAY_Employees" e where e."PersonId"=p."Id" and not e."IsDeleted" and e."Status" <> -1)
union all
select 'empleado vivo sin bandera', count(*) from dbo."COR_People" p where not p."IsEmployee" and not p."IsDeleted"
   and exists (select 1 from dbo."PAY_Employees" e where e."PersonId"=p."Id" and not e."IsDeleted" and e."Status" <> -1)
union all
select 'IsAssociate sin asociado', count(*) from dbo."COR_People" p where p."IsAssociate" and not p."IsDeleted"
   and not exists (select 1 from dbo."COR_Associates" a where a."PersonId"=p."Id" and not a."IsDeleted")
union all
select 'asociado sin bandera', count(*) from dbo."COR_People" p where not p."IsAssociate" and not p."IsDeleted"
   and exists (select 1 from dbo."COR_Associates" a where a."PersonId"=p."Id" and not a."IsDeleted")
union all
select 'personas eliminadas (soft)', count(*) from dbo."COR_People" p where p."IsDeleted"
union all
select 'personas total', count(*) from dbo."COR_People";
