using IngenIA365ERP.Shared.Services;

namespace IngenIA365ERP.Web.Services
{
    public class WebSecureStorage : ISecureStorage
    {
        private readonly Dictionary<string, string> _storage = new();

        public Task SetAsync(string key, string value)
        {
            _storage[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key)
        {
            _storage.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public bool Remove(string key)
        {
            return _storage.Remove(key);
        }

        public void RemoveAll()
        {
            _storage.Clear();
        }
    }
}
