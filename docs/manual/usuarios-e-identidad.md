# Usuarios e identidad

Cada persona tiene **una sola cuenta** en IngenIA365ERP, anclada a su documento de identidad, aunque trabaje
en varias cooperativas. Con esa cuenta entra a todas las que la invitaron, con la misma contraseña y el
mismo segundo factor.

## Para la persona

### Ingresar

En la pantalla de entrada se escribe el **correo** o el **usuario**, y la contraseña.

- El usuario es el número de documento, sin puntos ni espacios. Si el documento **no** es cédula de
  ciudadanía, va con el prefijo del tipo: `CE987654` (cédula de extranjería), `PPT…`, `PEP…`, `TI…`, `PA…`.
- Con una cédula se puede escribir con o sin puntos y con o sin «CC»: `1.234.567`, `1234567` y `CC1234567`
  son lo mismo.
- Si el correo o la clave no son correctos, la pantalla recuerda la regla del prefijo. Nunca dice si la
  cuenta existe.

### Registrar el documento

Quien ya tenía cuenta antes de que existiera el usuario de documento ve, al entrar, un aviso para
registrarlo. Se digita el tipo y el número dos veces. Se puede dejar «Para más tarde» hasta la fecha que fije
su cooperativa; desde esa fecha, el ingreso pide el documento antes de dejar entrar.

Si el sistema responde que **no pudo registrar ese documento**, es que ya está en otra cuenta. No se dice de
quién: se abre un caso para que soporte lo revise, y mientras tanto se sigue entrando con el correo.

### Mi cuenta

En el menú de la cuenta, «Mi cuenta» muestra el correo y el usuario de documento.

**Cambiar el correo** lo hace sólo la propia persona:

1. Confirmar que es ella: contraseña y código del segundo factor (o un código de respaldo).
2. Escribir el correo nuevo. Llega allí un código de seis dígitos que vence en 30 minutos.
3. Digitar el código. Desde ese momento se entra con el correo nuevo; al correo anterior le llega un aviso, y
   las demás sesiones abiertas se cierran.

### Olvidé mi contraseña

En la pantalla de entrada, «¿Olvidaste tu contraseña?»: se escribe el correo **o** el usuario. El enlace
llega siempre al correo de la cuenta y vence en una hora.

## Para el administrador de la cooperativa

### Invitar

Administración → Usuarios → «Invitar persona». Se digitan el tipo y el número de documento (dos veces),
los nombres, el correo, el celular y, si se quiere, el rol y el cargo.

- Si el documento es de alguien que ya tiene cuenta (por otra cooperativa), la invitación le llega **a su
  correo registrado**, no al digitado, y la persona entra con su misma contraseña. La pantalla responde igual
  en los dos casos.
- Si el correo digitado es de **otra** persona, la invitación se rechaza («Correo ya registrado»).
- Para corregir una invitación que salió mal, «Corregir»: sale una nueva y la anterior deja de servir.
- Lo mismo vale para el **primer administrador** de una cooperativa nueva, que registra el administrador de la
  plataforma: sin su documento, nombre y apellido la cooperativa no se crea.

### Invitaciones en revisión

Al aceptar, la persona confirma su número de documento. Si se equivoca las veces que fija la política, la
invitación queda **en revisión** y el administrador recibe un aviso. Se corrige la invitación (el documento
estaba mal digitado) o se envía una nueva.

### Corregir un documento

Usuarios → «Editar» → «Corregir documento»:

- **Corrección** cuando se digitó mal; **Actualización** cuando el documento cambió (de tarjeta de identidad
  a cédula, por ejemplo). El número se digita dos veces y las notas quedan en el historial.
- Sólo se puede si la persona está **únicamente en esta cooperativa**. Si también está en otra, la pantalla
  dice «Sólo lo puede hacer soporte» y se pide a soporte de la plataforma.
- La persona recibe un correo con su usuario nuevo. Su contraseña y su segundo factor no cambian.

En el listado de usuarios, una marca de **diferencia** indica que la copia del usuario de esta cooperativa no
coincide con la cuenta; se pone al día sola.

### Recuperar la contraseña de alguien

El administrador **no** fija contraseñas ni cambia correos. En Usuarios → «Editar» → «Enviar enlace de
recuperación», la persona recibe el enlace en su correo verificado y elige ella la contraseña.

### Retirar a alguien

«Deshabilitar» quita el acceso a **esta** cooperativa; si la persona trabaja en otras, las demás no se
tocan. Queda registrado quién la retiró.

### Política de identidad

Mi Cooperativa → «Política de identidad»:

- **Tipos de documento aceptados** al invitar.
- **Exigir desde**: la fecha desde la cual quien no registró su documento ya no puede entrar sin hacerlo.
  Vacía, no se exige nunca.
- **Intentos para confirmar el documento** al activar la invitación (de 1 a 10; por defecto 3).

## Para soporte de la plataforma

Consola SaaS → **Identidades**: buscar por correo, usuario o número; ver las cooperativas, el historial del
documento y los casos; corregir el documento sin límite de cooperativas; **cambiar el correo** cuando la
persona perdió su buzón (con motivo; avisa a los dos correos); y **fusionar** dos cuentas de la misma
persona (no se puede si están en una misma cooperativa). **Casos de identidad** lista los documentos tomados,
las fusiones en curso y los documentos que no coincidieron. Una fusión que quedó en curso se repite hasta
completarla.
