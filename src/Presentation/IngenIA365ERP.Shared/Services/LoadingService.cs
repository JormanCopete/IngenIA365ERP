namespace IngenIA365ERP.Shared.Services;

public interface ILoadingService
{
    event Action? OnChange;
    bool IsLoading { get; }
    void Show();
    void Hide();
}

public class LoadingService : ILoadingService
{
    public event Action? OnChange;
    public bool IsLoading { get; private set; }

    public void Show()
    {
        IsLoading = true;
        OnChange?.Invoke();
    }

    public void Hide()
    {
        IsLoading = false;
        OnChange?.Invoke();
    }
}
