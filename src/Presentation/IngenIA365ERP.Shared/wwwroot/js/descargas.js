// Feature 009 (T045). Entrega al navegador un archivo que el HttpClient autenticado del front ya
// descargó (un DotNetStreamReference): un navigate directo a la API no lleva el Authorization.
//
// Es un global (window.descargas), no un módulo ES: se carga UNA vez, con huella, desde App.razor
// (@Assets) y desde index.html del cliente MAUI, igual que preferencias.js, y lo usan los
// componentes de Shared (adjuntos de comprobantes) y la consola de auditoría. Vivía en
// Web.Client/wwwroot/audit-console.js, que sólo existía en el cliente WebAssembly.
window.descargas = {
    async downloadFromStream(fileName, contentType, streamRef) {
        const buffer = await streamRef.arrayBuffer();
        const blob = new Blob([buffer], { type: contentType });
        const url = URL.createObjectURL(blob);
        try {
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            a.remove();
        } finally {
            // Liberar el object URL en el siguiente tick, cuando el click ya inició la descarga.
            setTimeout(() => URL.revokeObjectURL(url), 0);
        }
    },

    // 2026-11-12: «Imprimir» un PDF que el front ya descargó (la cotización para el cliente): lo carga en un marco oculto y abre el
    // diálogo de impresión del navegador; si el visor de PDF no deja imprimir desde el marco, lo abre en otra pestaña para imprimirlo
    // desde allí. El interop se prueba a mano.
    async imprimirPdf(streamRef) {
        const buffer = await streamRef.arrayBuffer();
        const url = URL.createObjectURL(new Blob([buffer], { type: 'application/pdf' }));
        const marco = document.createElement('iframe');
        marco.setAttribute('aria-hidden', 'true');
        marco.style.position = 'fixed';
        marco.style.right = '0';
        marco.style.bottom = '0';
        marco.style.width = '0';
        marco.style.height = '0';
        marco.style.border = '0';
        marco.onload = () => {
            try {
                marco.contentWindow.focus();
                marco.contentWindow.print();
            } catch (e) {
                window.open(url, '_blank');
            }
            // El diálogo de impresión es asíncrono en algunos navegadores: el marco y el blob se liberan después.
            setTimeout(() => { marco.remove(); URL.revokeObjectURL(url); }, 60000);
        };
        marco.src = url;
        document.body.appendChild(marco);
    }
};
