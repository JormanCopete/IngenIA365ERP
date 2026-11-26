# 026 — Guía rápida (FDOSOL en una cooperativa de prueba)

1. **Línea**: Cartera → Líneas → FDOSOL → versión nueva con «Admite cargo masivo», valor por defecto 13.557, 1 cuota, mensual, caja. Sin
   codeudores, garantías ni documentos. «Se reporta a centrales» y «Entra a cobranza» en No.
2. **Matriz**: Contabilidad → Cartera → Matriz → plantilla: aparece la fila sugerida «Contrapartida del cargo» de FDOSOL. Ponerle la cuenta
   del fondo de solidaridad. La completitud deja de pedirla. El tipo `CO` ya está mapeado.
3. **Corrida**: Cartera → Procesos e informes → Cargos masivos → Nueva. Línea FDOSOL, período 2026-12, fecha del cargo 01/12, primer pago 31/12,
   tercero «el mismo asociado», origen «Asociados activos».
4. **Vista previa**: incluidos, omitidos por motivo, total y las dos cuentas. Confirmar.
5. **Ejecución**: la corrida pasa a «En curso» y luego a «Terminada». Cada asociado tiene su obligación `CR-…` de origen «Cargo masivo».
6. **Contabilidad**: un comprobante `CO` con débito por asociado a la cuenta por cobrar de FDOSOL y crédito a la contrapartida.
7. **Cobro**: en Caja, buscar un asociado: la obligación aparece con su cuota del 31/12.
8. **Repetir**: una corrida nueva del mismo período omite a todos por «ya tiene el cargo de este período».
9. **Anular**: anular el cargo de un asociado sin pagos; queda «Anulada» y un comprobante `CO` de reverso. Volver a cargarlo funciona.

Pruebas automáticas:

```powershell
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~CargosMasivos"
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~CargosMasivos"
dotnet test tests/IngenIA365ERP.Architecture.Tests
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~CargosMasivos"
```
