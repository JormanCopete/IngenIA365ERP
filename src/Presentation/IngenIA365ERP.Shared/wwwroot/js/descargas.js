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
    }
};
