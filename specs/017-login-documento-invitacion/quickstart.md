# Quickstart — validar la 017

Recorrido para comprobar a mano, en local o en QA, que la feature cumple la spec. Las e2e automatizan
lo mismo y se corren al final.

## 0. Antes de desplegar en un ambiente

1. Correr [diagnostico-de-datos.md](diagnostico-de-datos.md) en la base administrativa y en cada
   cooperativa, y anotar los resultados.
2. Si A1 (correos duplicados) o B6 (documentos que chocan al normalizar) devuelven filas, **no se
   despliega**: las migraciones se detienen con esa lista. Primero se resuelve cada caso con el dueño.
3. Respaldo de la base administrativa y de cada cooperativa (Principio XII).

## 1. La regla de oro (US1)

1. Antes de desplegar, abrir una sesión con una cuenta existente y dejar la pestaña abierta.
2. Desplegar: migraciones, más la tarea `identidad-documento-desde-cooperativas`.
3. La pestaña renueva sola y sigue trabajando, sin volver al login.
4. Salir y entrar con el mismo correo y la misma clave. Debe pedir el mismo segundo factor y mostrar
   las mismas cooperativas y permisos.
5. Si la cuenta no tenía documento, aparece el paso «Registra tu documento»; se pospone y se sigue
   trabajando.

## 2. Invitar y activar (US2, US3)

1. Como administrador de la cooperativa A: Usuarios › Invitaciones › Nueva. Llenar CC 1.234.567 (dos
   veces), nombres, apellidos, correo personal, celular, rol «Operador» y un cargo.
2. Repetir con el correo de otra cuenta. Debe responder «Correo ya registrado».
3. Abrir el enlace. En la pantalla no aparece el documento.
4. Digitar mal el número dos veces: queda un intento.
5. Digitar `1.234.567`, crear la clave e inscribir el segundo factor. Debe aparecer «Puedes ingresar
   con tu correo o con tu usuario: **1234567**».
6. La persona queda con rol Operador, el cargo elegido y su persona en la cooperativa A enlazada (o
   creada).
7. Desde la cooperativa B, invitar el mismo documento con otro correo. El mensaje es el mismo, pero el
   correo llega al de la persona. Al aceptar, entra a B con su clave de siempre.
8. Desde la cooperativa C, invitar y fallar el número tres veces. La invitación queda «En revisión»
   y el administrador recibe el aviso.

## 3. Entrar con usuario (US4)

| Escribe | Resultado |
|---|---|
| `1234567`, `1.234.567`, `1 234 567`, `CC1234567` | entra |
| `ce1234567` (si no existe CE 1234567) | «Credenciales inválidas» y la pista |
| `PA-ab12345` para el pasaporte AB12345 | entra |
| un correo sin verificar | «Credenciales inválidas» |
| cinco claves malas alternando correo y usuario | bloqueo a la quinta, no a la décima |

## 4. Administración (US5–US8)

1. Corrección por el administrador:
   - Corregir el documento de una persona que sólo está en A: pide motivo, cambia el usuario y deja
     historial; la persona recibe el correo con su nuevo usuario.
   - Intentarlo con una persona que está en A y B: «Sólo lo puede hacer soporte».
2. Como maestro:
   - Fusionar dos identidades de prueba. Las membresías pasan a la correcta y la otra no entra.
   - Rescatar un correo.
3. Como persona, en Mi cuenta › Cambiar correo:
   - Pide clave, segundo factor y el código. El correo anterior recibe el aviso.
   - Con un correo ya registrado, lo rechaza.
4. Retirar a una persona de A con sesión abierta en A: se corta a más tardar en la siguiente renovación
   y sigue entrando a B.
5. En la pantalla de usuarios ya no existe «Restablecer clave», sólo «Enviar enlace de recuperación».

## 5. Migración idempotente

Volver a correr el DbMigrator y la tarea. No cambia ninguna fila y el reporte de conflictos sale igual.
