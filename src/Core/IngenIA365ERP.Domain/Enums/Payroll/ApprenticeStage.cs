namespace IngenIA365ERP.Domain.Enums.Payroll;

/// <summary>
/// Etapa del contrato de aprendizaje (Ley 2466 de 2025). Decide prestaciones y cotizante PILA:
/// en la lectiva no hay prestaciones y el apoyo es parcial; en la práctica el aprendiz cotiza
/// como dependiente. Obligatoria en la ficha si la clase es aprendiz o pasante.
/// </summary>
public enum ApprenticeStage
{
    Lective = 1,
    Practical = 2,
}
