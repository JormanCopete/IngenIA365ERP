namespace IngenIA365ERP.Domain.Enums.Compliance;

/// <summary>
/// Estado de una solicitud del titular (feature 013, T69, S10; data-model parte 3 §9.2). Se guarda como int en
/// <c>COR_DataSubjectRequests.Status</c>. La prórroga (<see cref="Extended"/>) se concede una sola vez.
/// </summary>
public enum DataSubjectRequestStatus
{
    Received = 1,
    Extended = 2,
    Answered = 3,
    Withdrawn = 4,
}
