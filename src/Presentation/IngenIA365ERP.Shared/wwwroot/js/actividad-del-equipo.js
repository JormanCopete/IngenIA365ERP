// Uso del computador para el cierre de sesión por inactividad (parámetro Security.Session.CountDeviceActivity).
//
// La página no puede ver qué programas se usan; la Idle Detection API (Chrome y Edge) sólo dice si hay teclado o ratón en
// uso en el equipo en el último minuto («active»/«idle») y si la pantalla está bloqueada. Exige permiso de la persona, que
// se pide con un clic (RenovadorDeSesion / AvisoDeVencimientoDeSesion). Firefox y Safari no la tienen: ahí todo sigue como
// antes. Lo lee el latido de un segundo del aviso de sesión.
window.actividadDelEquipo = (function () {
    const soportado = 'IdleDetector' in window;
    const estado = { soportado: soportado, permiso: soportado ? 'desconocido' : 'no-soportado', enUso: false, pantallaBloqueada: false, vigilando: false };
    let detector = null;
    let control = null;

    async function consultarPermiso() {
        try {
            const p = await navigator.permissions.query({ name: 'idle-detection' });
            return p.state; // granted | denied | prompt
        } catch (e) {
            return 'desconocido';
        }
    }

    function copiar() {
        if (detector) {
            estado.enUso = detector.userState === 'active';
            estado.pantallaBloqueada = detector.screenState === 'locked';
        }
    }

    async function arrancar() {
        if (!soportado || detector) return;
        try {
            control = new AbortController();
            const d = new IdleDetector();
            d.addEventListener('change', copiar);
            await d.start({ threshold: 60000, signal: control.signal });
            detector = d;
            estado.vigilando = true;
            copiar();
        } catch (e) {
            detector = null;
            estado.vigilando = false;
        }
    }

    return {
        leer: async function () {
            if (soportado && !estado.vigilando) {
                estado.permiso = await consultarPermiso();
                if (estado.permiso === 'granted') await arrancar();
            }
            return Object.assign({}, estado);
        },
        pedirPermiso: async function () {
            if (!soportado) return 'no-soportado';
            try {
                estado.permiso = await IdleDetector.requestPermission();
            } catch (e) {
                estado.permiso = 'denied';
            }
            if (estado.permiso === 'granted') await arrancar();
            return estado.permiso;
        },
        detener: function () {
            if (control) control.abort();
            detector = null;
            control = null;
            estado.vigilando = false;
            estado.enUso = false;
        }
    };
})();
