# Propuesta de enmienda a la constitución — Sistemas satélite (feature 021)

**Estado**: propuesta, **no aplicada**. La constitución vigente es la v2.0.1 (2026-10-23). Esta enmienda es la
entrega **C0** de la feature 021 y su gobierno exige **dos revisores** (el dueño y un segundo revisor) antes de
escribirla en `.specify/memory/constitution.md`. Mientras no se apruebe, nada del canal se construye.

**Versión propuesta**: **2.1.0 (MINOR)**. Agrega una sección nueva y un prefijo; no redefine ni elimina ningún
principio, y no cambia una sola línea de lo que rige al ERP.

## Motivación

El dueño decidió (2026-11-08, D1–D2 de la spec 021) que el canal digital del asociado sea un servicio aparte,
escrito en Go, con interfaz React empaquetada en MAUI HybridWebView. La constitución declara un solo stack
(.NET 10, Blazor, Carter, MediatR) y sus doce principios están redactados para él: aplicados literalmente al
canal, los Principios II y III no tienen sentido (no hay MediatR en Go) y el Principio IV prohibiría lo que el
canal necesita (conocer a la persona en varias cooperativas).

## Texto propuesto

Se agrega, entre **Estándares Técnicos Adicionales** y **Flujo de Desarrollo y Compuertas de Calidad**:

> ## Sistemas satélite
>
> Un **sistema satélite** es un servicio que vive en el mismo repositorio y el mismo clúster que el ERP pero
> fuera de su arquitectura en capas, con su propio lenguaje, su propia base y su propio ciclo de despliegue.
> Hoy hay uno: el **canal digital del asociado** (`canal/`, feature 021), en Go, con interfaz React y una
> envoltura MAUI HybridWebView para las tiendas. Crear otro exige una spec que lo justifique y una enmienda que
> lo nombre aquí.
>
> Reglas vinculantes para todo satélite:
>
> 1. **NEVER** abre una conexión a la base de una cooperativa, a la base administrativa, a su Mongo ni a su
>    Redis. Todo lo que necesita del ERP lo pide por una API de servicio del ERP, versionada, y todo lo que
>    cambia en el ERP lo pide por esa API. El ERP sigue siendo el **único** que escribe en sus libros, y lo hace
>    por sus propios comandos MediatR, con su `AuditBehavior` (Principios III y X intactos dentro del ERP).
> 2. Toda llamada entre el satélite y el ERP va autenticada en **ambos sentidos** con un token de servicio
>    firmado, de vida corta y con identificador único que no se acepta dos veces; la cooperativa viaja en la ruta
>    y en el token, y el ERP la resuelve igual que para un usuario (Principio IV: nunca sin cooperativa).
> 3. Toda escritura que el satélite pide al ERP es **idempotente** por una llave de negocio del satélite:
>    repetirla no cambia nada y devuelve el mismo resultado.
> 4. El satélite guarda de una cooperativa sólo lo indispensable para operar —identificador público de la
>    persona, documento, estado de habilitación— y **NEVER** saldos, movimientos ni datos contables. Lo que
>    guarda de una persona en una cooperativa no se le muestra a otra.
> 5. Hacia afuera viaja sólo el `PublicId` del ERP (Principio VI). Los identificadores propios del satélite
>    también son opacos (UUID).
> 6. El satélite cumple, con sus propios medios, la **intención** de los Principios VII a IX y XII: nada se borra
>    físicamente en flujo de usuario (estados y fechas en lugar de DELETE), validación en cliente y en servidor
>    con mensajes en español y códigos con espacio de nombres, ningún error silenciado (log estructurado con
>    cooperativa, operación y correlación; sin PII innecesaria), y migraciones versionadas, reversibles o
>    declaradas irreversibles. Las acciones de seguridad y de dinero quedan en su propia auditoría, sólo de
>    anexar, con retención mínima de 5 años.
> 7. Credenciales y secretos de terceros (pasarelas, proveedores) se guardan **cifrados** con llave fuera de la
>    base, nunca en el repositorio, nunca en logs, nunca devueltos completos por una API.
> 8. Claves de personas: hash adaptativo con costo equivalente o mayor que el del ERP (BCrypt ≥ 11).
> 9. Las pruebas del satélite viven con él (`canal/**/…_test.go`, pruebas del frontend) y el CI las corre en
>    cada cambio de su carpeta; las pruebas de contrato con el ERP viven en ambos lados.
>
> Lo que **no** cambia: los doce principios siguen rigiendo al ERP sin excepción. La API de servicio que el ERP
> expone al satélite es código del ERP y cumple los doce.

Y en **Estándares Técnicos Adicionales → Tablas SQL**, se agrega el prefijo **`CDG_`** (canal digital) a la
lista.

## Impacto

- **Código existente**: ninguno.
- **CI**: un flujo nuevo para `canal/**`, independiente del de la API.
- **Plantilla del plan**: en una feature que toque un satélite, la Constitution Check agrega la fila
  «Satélite (reglas 1–9)».

## Plan de migración

No aplica: no hay código existente que se deba ajustar.
