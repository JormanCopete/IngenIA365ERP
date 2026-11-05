# Configuración por cooperativa

Una carpeta por cooperativa (`cooperativas/<codigo>/`) con:

- `perfiles/*.json`: un perfil por archivo que entrega el sistema anterior;
- `equivalencias/*.csv`: las tablas de equivalencias (`codigoAnterior,codigoNuevo,clase,nota`);
- `cifras/*.csv`: las cifras de referencia por archivo (`total,valor`), si no tienen datos personales.

**El repositorio es público: estas carpetas no se suben** (están en `.gitignore`). Llevan documentos, saldos y equivalencias de la
cooperativa y se guardan fuera de GitHub. Nada de planos ni de libros tampoco: van a la carpeta de trabajo. La `version` de un perfil
sube cuando ya se usó en una carga aplicada.
