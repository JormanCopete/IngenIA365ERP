# Guía rápida — 028 Recogida de créditos

Para probar a mano en local (API + Web con PostgreSQL nativo, `AutoMigrate`), con una cooperativa con Cartera activada.

1. **Datos**: un asociado con dos créditos al día (uno por caja, uno por libranza con su cuota del mes en un archivo de descuentos marcado
   enviado) y uno en mora.
2. **Parámetros de fábrica**: en `/cartera/parametros` no hace falta tocar nada; todo se comporta como hoy.
3. **Vender**: `/cartera/solicitudes/nueva` → elegir al asociado → simular un crédito de $8.000.000 → la sección «Lo que recoge» lista los tres
   créditos; el de mora sale sin poder marcarse. Marcar los dos al día; cambiar la fecha proyectada de desembolso y ver cambiar el neto. El de
   libranza muestra la cuota enviada y qué pasará con ella.
4. **Radicar** y abrir el expediente: «Lo que recoge» con lo proyectado; la capacidad de pago dice cuánto liberó la recogida.
5. **Decidir** con otra persona.
6. **Cambiar algo**: pagar una cuota en caja del crédito por caja.
7. **Desembolsar** en `/cartera/desembolsos?solicitud=…`: la vista previa muestra la diferencia del crédito pagado con su causa «Pagos»;
   marcar «Revisé las diferencias» y desembolsar. Comprobar:
   - los dos créditos viejos cancelados («recogido por el crédito …» en su detalle);
   - la orden de giro por el neto mostrado;
   - en Contabilidad, el DS con «desembolso por girar» por el neto y el puente de la recogida en cero.
8. **Cuota causada (E2)**: poner `Recogida.CuotaCausada = Excluir` en la línea de libranza y repetir: el crédito de libranza queda vivo sólo con
   la cuota enviada; registrar la respuesta y el giro de la pagaduría: queda cancelado sin saldo a favor ni alerta.
9. **Caja (E2)**: con `Pagos.CuotaEnviadaAlCancelar = Preguntar`, F7 sobre un crédito con cuota enviada ofrece «todo menos la cuota enviada».
10. **Informe**: `/cartera/informes?vista=payoffs` lista el desembolso con sus diferencias; exportar a Excel.

Compilar con `dotnet build IngenIA365ERP.slnx -m:1`. Pruebas unitarias por proyecto (`dotnet test tests/IngenIA365ERP.<X>.Tests -m:1`); las e2e
(`tests/IngenIA365ERP.API.IntegrationTests`, colección de Cartera) una sola vez al final, con Docker Desktop y en los dos motores.
