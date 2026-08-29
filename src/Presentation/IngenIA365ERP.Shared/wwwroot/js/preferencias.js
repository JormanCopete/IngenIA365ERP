/* =============================================================================
   Preferencias visuales del usuario.

   Único lugar donde se aplica tema, densidad, escala y contraste. Los dos hosts
   (web y MAUI) y el servicio de Blazor llaman acá, para que no existan dos
   implementaciones que se desincronicen.
   ========================================================================== */

window.erpPreferencias = (function () {
    const CLAVE = 'erp-preferencias';

    function leer() {
        try {
            return JSON.parse(localStorage.getItem(CLAVE) || '{}');
        } catch (e) {
            return {};
        }
    }

    function guardar(p) {
        try {
            localStorage.setItem(CLAVE, JSON.stringify(p));
        } catch (e) {
            /* Modo privado o almacenamiento lleno: la preferencia igual se
               aplica en esta sesión y se sincroniza contra el servidor. */
        }
    }

    /* Cambiar el tema sin esto deja elementos con el color anterior.
       Motivo: las propiedades con `transition` cuyo valor sale de una variable
       CSS no siempre se recalculan al cambiar la variable — el navegador
       conserva el último valor pintado. Se comprobó en el botón del login: la
       variable ya valía el morado del tema oscuro y el botón seguía mostrando
       el del tema claro.

       Apagar las transiciones durante el cambio resuelve las dos cosas a la
       vez: fuerza el recálculo y evita que TODA la interfaz haga una animación
       de color a la vez, que se ve como un parpadeo. */
    function sinTransiciones(fn) {
        const raiz = document.documentElement;
        raiz.classList.add('sin-transiciones');
        fn();
        /* Leer una propiedad de layout obliga al navegador a recalcular estilos
           ahora, antes de volver a permitir transiciones. Sin esta línea el
           navegador agrupa ambos cambios y la clase no llega a tener efecto. */
        void raiz.offsetHeight;
        raiz.classList.remove('sin-transiciones');
    }

    function aplicar(p) {
        const raiz = document.documentElement;
        sinTransiciones(function () {
            const tema = p.tema || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'oscuro' : 'claro');
            raiz.setAttribute('data-tema', tema);

            if (p.densidad)  { raiz.setAttribute('data-densidad', p.densidad); }  else { raiz.removeAttribute('data-densidad'); }
            if (p.escala)    { raiz.setAttribute('data-escala', p.escala); }      else { raiz.removeAttribute('data-escala'); }
            if (p.contraste) { raiz.setAttribute('data-contraste', p.contraste); } else { raiz.removeAttribute('data-contraste'); }

            /* Los dos temas de SyncFusion están cargados; se habilita uno.
               Alternar `disabled` es instantáneo y no vuelve a descargar nada. */
            const claro = document.getElementById('sf-tema-claro');
            const oscuro = document.getElementById('sf-tema-oscuro');
            if (claro && oscuro) {
                claro.disabled = tema === 'oscuro';
                oscuro.disabled = tema !== 'oscuro';
            }
        });
    }

    return {
        leer: leer,

        /* Aplica y persiste en el navegador. La sincronización contra el
           servidor la hace el servicio de Blazor: acá se guarda primero para
           que la preferencia sobreviva a una recarga aunque la red falle. */
        establecer: function (p) {
            const actual = leer();
            const nuevo = Object.assign({}, actual, p);
            guardar(nuevo);
            aplicar(nuevo);
            return nuevo;
        },

        /* Para cuando el servidor devuelve las preferencias guardadas de otro
           dispositivo: pisa lo local sin volver a escribir el servidor. */
        aplicarDesdeServidor: function (p) {
            guardar(p);
            aplicar(p);
        },

        aplicar: function () { aplicar(leer()); }
    };
})();
