using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Core;

public class CoreMasterService<TDto> where TDto : class
{
    private readonly HttpClient _http;
    private readonly string _basePath;

    public CoreMasterService(HttpClient http, string basePath)
    {
        _http = http;
        _basePath = basePath;
    }

    public async Task<List<TDto>> ListAsync(string? search = null)
    {
        var url = $"{_basePath}?pageSize=500";
        if (!string.IsNullOrEmpty(search)) url += $"&searchTerm={search}";
        try
        {
            var result = await _http.GetFromJsonAsync<PagedResult<TDto>>(url);
            return result?.Items ?? [];
        }
        catch { return []; }
    }

    public async Task<TDto?> GetAsync(Guid id)
    {
        try { return await _http.GetFromJsonAsync<TDto>($"{_basePath}/{id}"); }
        catch { return null; }
    }

    public async Task<HttpResponseMessage> CreateAsync(object request)
        => await _http.PostAsJsonAsync(_basePath, request);

    public async Task<HttpResponseMessage> UpdateAsync(Guid id, object request)
        => await _http.PutAsJsonAsync($"{_basePath}/{id}", request);

    public async Task<HttpResponseMessage> DeleteAsync(Guid id)
        => await _http.DeleteAsync($"{_basePath}/{id}");
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
