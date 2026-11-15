/* Feature 021, US10 (T132): los avisos por Web Push en la PWA instalada. El service worker de Workbox importa este archivo.
   El canal cifra el aviso para este navegador; aquí sólo se muestra y, al tocarlo, se abre la pantalla que trae. */
self.addEventListener('push', (evento) => {
  let datos = {}
  try {
    datos = evento.data ? evento.data.json() : {}
  } catch {
    datos = { title: 'Canal del asociado', body: evento.data ? evento.data.text() : '' }
  }
  evento.waitUntil(
    self.registration.showNotification(datos.title || 'Canal del asociado', {
      body: datos.body || '',
      icon: '/iconos/icono-192.png',
      tag: datos.kind || 'aviso',
      data: { url: datos.url || '/' },
    }),
  )
})

self.addEventListener('notificationclick', (evento) => {
  evento.notification.close()
  const destino = new URL((evento.notification.data && evento.notification.data.url) || '/', self.location.origin)
  // Sólo se abren pantallas del propio canal.
  const url = destino.origin === self.location.origin ? destino.href : self.location.origin + '/'
  evento.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((ventanas) => {
      for (const v of ventanas) {
        if ('focus' in v) {
          v.navigate(url)
          return v.focus()
        }
      }
      return self.clients.openWindow(url)
    }),
  )
})
