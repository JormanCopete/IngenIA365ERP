using System.Runtime.InteropServices;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.App.Platforms.Windows;

/// <summary>
/// En la aplicación de escritorio de Windows el uso del computador se pregunta al sistema (GetLastInputInfo): la última vez
/// que hubo teclado o ratón en cualquier programa. No hace falta permiso. Con la pantalla bloqueada no hay entrada, así que
/// al minuto deja de contar, igual que en el navegador.
/// </summary>
public sealed class ActividadDelEquipoWindows : IActividadDelEquipo
{
    private static readonly TimeSpan Umbral = TimeSpan.FromMinutes(1);

    public Task<EstadoDelEquipo> LeerAsync()
    {
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return Task.FromResult(EstadoDelEquipo.NoSoportado);
        var inactivo = TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
        return Task.FromResult(new EstadoDelEquipo(true, true, false, inactivo < Umbral, false));
    }

    public Task<bool> PedirPermisoAsync() => Task.FromResult(true);

    public Task DetenerAsync() => Task.CompletedTask;

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);
}
