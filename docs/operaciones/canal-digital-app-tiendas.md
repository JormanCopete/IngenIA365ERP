# Canal digital — la app de tiendas (feature 021, US11)

La app del canal del asociado (`canal/app`, .NET 10 MAUI) es **la misma PWA** (`canal/web`, build en modo «app») dentro de un
HybridWebView, con un puente que da lo que el navegador no da: biometría, almacenamiento seguro, la pasarela en el navegador del
sistema con retorno por App Link / Universal Link, notificaciones por FCM y guardar o compartir PDF. Es otra app que la de
funcionarios: `ApplicationId` **`com.ingenia365.canal`**, otra firma.

**Este repositorio es público**: ninguna llave de firma, perfil de aprovisionamiento, `google-services.json`,
`GoogleService-Info.plist` ni cuenta de servicio va al repositorio ni al CI público. Todo eso vive en el equipo de quien publica (o en
un almacén de secretos privado) y la publicación es **manual**.

## Versión mínima soportada

| Plataforma | Mínima | Por qué |
|---|---|---|
| Android | 7.0 (API 24) | La de MAUI 10. La biometría se ofrece desde Android 10 (API 29, `BiometricPrompt` del sistema); antes, la persona entra con su clave. |
| iOS | 15 | La de MAUI 10. Face ID / Touch ID con `LAContext`. |

## Antes de la primera publicación

1. **Cuentas de desarrollador** a nombre de la empresa: Google Play Console y Apple Developer Program (equipo y `TEAMID`).
2. **Firebase** (para los avisos en el teléfono): un proyecto, una app Android `com.ingenia365.canal` y una iOS con el mismo bundle; la
   llave APNs de Apple cargada en Firebase. Bajar `google-services.json` y `GoogleService-Info.plist` a `canal/app/Platforms/Android/` y
   `canal/app/Platforms/iOS/` (git los ignora). La cuenta de servicio para el servidor del canal va al secreto
   `CANAL_FCM_CUENTA_DE_SERVICIO` del ambiente (nunca al repositorio).
3. **Firma de Android**: un keystore de subida (`*.jks`, fuera del repositorio) y la firma de apps de Google Play. La huella SHA-256
   del certificado **con que Google firma** (Play Console → Integridad de la app) va a `CANAL_ANDROID_HUELLAS` del canal; sin ella, Android
   no verifica los App Links y el retorno de la pasarela abre el navegador en vez de la app.
4. **iOS**: el App ID con «Associated Domains» y «Push Notifications»; el `TEAMID.com.ingenia365.canal` va a `CANAL_IOS_APP_ID` del
   canal. `Platforms/iOS/Entitlements.plist` trae `applinks:asociados.ingenia365.com`; si el host del ambiente es otro, cambiarlo al
   firmar.
5. Comprobar que el canal publica `https://<host>/.well-known/assetlinks.json` y `/.well-known/apple-app-site-association` (sin las
   variables responden 404).

## Construir y publicar (manual)

```bash
CANAL_HOST=asociados.ingenia365.com canal/scripts/empaquetar-app.sh
```

```bash
dotnet publish canal/app -f net10.0-android -c Release -p:CanalHost=asociados.ingenia365.com -p:CanalConFirebase=true -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=<ruta al .jks> -p:AndroidSigningKeyAlias=<alias> -p:AndroidSigningKeyPass=env:CANAL_CLAVE_KEYSTORE -p:AndroidSigningStorePass=env:CANAL_CLAVE_KEYSTORE
```

El `.aab` de `bin/Release/net10.0-android/publish` se sube a Play Console (prueba interna primero). iOS se construye en una Mac con
Xcode y el perfil de distribución (`dotnet publish -f net10.0-ios -c Release -p:CanalHost=… -p:CanalConFirebase=true`) y se sube con
Transporter a App Store Connect (TestFlight primero).

`CanalConFirebase=true` incluye `Plugin.Firebase.CloudMessaging` (la versión del `.csproj` se revisa contra NuGet antes de la primera
publicación); sin él la app compila y funciona, sin avisos en el teléfono (llegan por correo).

## Lo que hay que probar a mano en cada versión

- Ingreso con documento y clave; activar «entrar con huella / rostro» y volver a entrar con la biometría.
- Un pago: la pasarela abre en Custom Tabs / la hoja de Safari y, al volver, la app muestra el resultado (si abre el navegador, falta
  la verificación del App Link: revisar las huellas o el App ID).
- Descargar un comprobante y un extracto: se comparten o guardan con la hoja del sistema.
- «Mi perfil › Avisos › Activar notificaciones»: llega un aviso de prueba y tocarlo abre la pantalla que trae.

## Revocar

Un teléfono perdido: el asociado (o la atención de la cooperativa, «Cerrar sesiones») cierra sus sesiones; el dispositivo de confianza
se revoca desde «Mi perfil › Dispositivos». Una llave de firma comprometida: rotarla en la consola de la tienda y cambiar
`CANAL_ANDROID_HUELLAS` (las dos huellas conviven mientras dure el cambio).
