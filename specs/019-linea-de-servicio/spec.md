# 019 — Línea de servicio en Cartera

## Problema

Todas las líneas de colocación cuentan como crédito. Una cooperativa puede llevar en Cartera cuentas por cobrar de servicios, y hoy esas obligaciones entran a los informes regulatorios, al deterioro general y al arrastre de calificación como si fueran crédito.

## Decisión

Una marca `IsService` por línea, en `LND_CreditLines`, apagada por defecto y por cooperativa. Va en la línea y no en su versión: se puede poner con la línea activa, sin versión nueva, y alcanza a obligaciones vivas y migradas.

## Requisitos

- **FR-001** Quien tiene `Lending.CreditLines.Manage` marca y desmarca una línea de colocación desde `/cartera/lineas/{id}`, con motivo. El comando queda auditado.
- **FR-002** Una línea de captación o de aportes no admite la marca (`Lending.CreditLine.NatureMismatch`).
- **FR-003** La obligación de una línea marcada queda fuera de la proyección del SICSES. El cierre mensual la sigue conteniendo.
- **FR-004** Queda fuera del archivo de centrales de riesgo.
- **FR-005** Queda fuera de la base del deterioro general.
- **FR-006** Queda fuera del arrastre de calificación en las dos direcciones, y de la alineación por deudor de pérdida esperada.
- **FR-007** No cambian el deterioro individual, la cobranza, el aviso previo, la causación ni la mora.
- **FR-008** El cierre sella la marca en `LND_MonthlyCloseLoans.IsService`: mover la marca después no altera un cierre hecho. El SICSES y el deterioro general leen ese sello.

## Límites

- Las centrales no parten de un cierre: leen la marca vigente al generar. Una obligación ya reportada que se marque después sale del archivo siguiente sin novedad de retiro.
- El tablero y el informe de calidad siguen contando las obligaciones de servicio.
- La plantilla `lending.credit-lines` no lleva la marca: esa plantilla carga versiones y la marca no es de la versión.

## Migración

`LineaDeServicio`, nueva y aditiva, en los dos proveedores: dos columnas `bool` con defecto `false`. No toca las migraciones congeladas.
