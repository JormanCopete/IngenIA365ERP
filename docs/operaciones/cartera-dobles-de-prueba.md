# Cartera: los dobles de prueba (retirados)

> Feature 015 (Recaudo y Tesorería), T0306–T0309. Antes, en la feature 013, este runbook explicaba cómo usar la **tesorería de prueba**
> (`TES`) y el **recaudador de prueba** (`TST`) fuera de producción mientras Recaudo y Tesorería no existían. Con la 015 ya existen, y
> los dobles **se retiraron en todo ambiente**: no hay código, ni pantalla (`/cartera/dobles-de-prueba`), ni rutas
> (`/api/lending/test-doubles/*`), ni permiso (`Lending.TestDoubles.Operate`), ni bandera (`Lending:TestDoubles:Enabled`).
> La prueba de arquitectura `NoHayDoblesDePrueba` impide que vuelvan.

## 1. Qué hace la migración

`RetiroDeLosDoblesDePrueba` (PostgreSQL `20261027024928`, SQL Server `20261027024843`) es **sólo de datos** y se aplica sola al desplegar
(`AutoMigrate`, cooperativa por cooperativa):

- cierra `ValidTo` (hoy, o `ValidFrom` si fuera posterior) de cada participante de integración vigente que sea un doble
  (`IsTestDouble` o clase `Test`) y de sus adopciones vigentes;
- no borra nada ni toca `IsEnabled` ni `Reason` (`COR_IntegrationParticipants` es un hecho inmutable);
- es idempotente y en producción no hace nada, porque allí nunca se registraron dobles;
- su `Down()` no los reabre.

## 2. Cómo se ven después

En `/admin/participantes-de-integracion`, `TES` y `TST` aparecen con su vigencia **cerrada** en la fecha del despliegue; ya no cuentan para
la activación de Cartera ni de Captaciones y aportes. Un requisito que dependía de ellos vuelve a «pendiente» hasta que se registre lo real.

## 3. Cómo se registra lo real

En DEV y QA, igual que en producción, el recaudador y la tesorería son los módulos de la 015:

1. Poner en marcha Recaudo y Tesorería en la cooperativa: [recaudo.md](../manual/recaudo.md) y su runbook de puesta en marcha.
2. Verificar en `/admin/participantes-de-integracion` el recaudador (contrato de obligaciones) y el destino `Treasury` vigentes.
3. Volver a `/cartera/activacion` (y a la de captaciones): los requisitos de recaudador y tesorería quedan cumplidos.

La bandera `Lending:TestDoubles:Enabled` nunca estuvo en los manifiestos de GitOps de DEV ni de QA, ni en sus ConfigMaps (verificado el
2026-10-28), así que la parte de GitOps de T0309 no tuvo nada que quitar.
