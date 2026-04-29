namespace IngenIA365ERP.Shared.Services
{
    public interface ISecureStorage
    {
        Task SetAsync(string key, string value);
        Task<string?> GetAsync(string key);
        bool Remove(string key);
        void RemoveAll();
    }
}
