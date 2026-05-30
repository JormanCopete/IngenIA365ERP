// T092 — Helper de descarga para la consola de audit log.
//
// Recibe un DotNetStreamReference (bytes producidos por el HttpClient
// autenticado del front) y lo materializa como descarga del browser.
// Usamos esta vía en lugar de un navigate directo a /api/audit/logs/export.*
// porque el navigate del browser NO incluye el header Authorization del
// access token JWT; el fetch interno sí lo lleva via HttpClient configurado.

export async function downloadFromStream(fileName, contentType, streamRef) {
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
        // Liberar el object URL en el siguiente tick para asegurar que el
        // click llegó a iniciar la descarga.
        setTimeout(() => URL.revokeObjectURL(url), 0);
    }
}
