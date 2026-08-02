namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// T091a/feature 003 — Estado global de "formularios con cambios sin
/// guardar" por sesión de usuario (scoped). Lo alimenta
/// <c>DirtyTrackingEditForm</c> y lo consulta el <c>TenantSwitcher</c> antes
/// de cambiar de empresa (FR-103): si hay formularios sucios, se pide
/// confirmación antes de descartar.
/// </summary>
public interface IFormDirtyStateService
{
    /// <summary>Marca un formulario como sucio (campo modificado sin guardar).</summary>
    void MarkDirty(string formId);

    /// <summary>Marca un formulario como limpio (guardado o descartado).</summary>
    void MarkClean(string formId);

    /// <summary>true si algún formulario de la sesión tiene cambios sin guardar.</summary>
    bool HasDirtyForms { get; }

    /// <summary>Limpia todo (p. ej. tras un cambio de empresa confirmado).</summary>
    void Clear();
}

public sealed class InMemoryFormDirtyStateService : IFormDirtyStateService
{
    private readonly HashSet<string> _dirty = [];
    private readonly object _gate = new();

    public void MarkDirty(string formId)
    {
        lock (_gate) _dirty.Add(formId);
    }

    public void MarkClean(string formId)
    {
        lock (_gate) _dirty.Remove(formId);
    }

    public bool HasDirtyForms
    {
        get { lock (_gate) return _dirty.Count > 0; }
    }

    public void Clear()
    {
        lock (_gate) _dirty.Clear();
    }
}
