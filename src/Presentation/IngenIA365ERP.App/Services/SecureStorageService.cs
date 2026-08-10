using IngenIA365ERP.Shared.Services;

namespace IngenIA365ERP.App.Services
{
    public class SecureStorageService : Shared.Services.ISecureStorage
    {
        public async Task SetAsync(string key, string value)
        {
            await SecureStorage.Default.SetAsync(key, value);
        }

        public async Task<string?> GetAsync(string key)
        {
            return await SecureStorage.Default.GetAsync(key);
        }

        public bool Remove(string key)
        {
            return SecureStorage.Default.Remove(key);
        }

        public void RemoveAll()
        {
            SecureStorage.Default.RemoveAll();
        }
    }
}
