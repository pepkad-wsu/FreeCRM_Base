using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CICD.Services
{
    public interface IIISInfoProvider
    {
        /// <summary>
        /// Reads the IIS info JSON files from disk and returns their strongly-typed objects.
        /// </summary>
        /// <returns>
        /// A dictionary with keys "AzureCMS", "AzureDev", and "AzureProd" mapping to an IISInfo object,
        /// or missing keys if the corresponding file does not exist.
        /// </returns>
        Task<Dictionary<string, DataObjects.IISInfo?>> GetIISInfoAsync();
    }

    public class IISInfoProvider : IIISInfoProvider
    {
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;

        public IISInfoProvider(IWebHostEnvironment env, IMemoryCache memoryCache)
        {
            _env = env;
            _cache = memoryCache;
        }

        public async Task<Dictionary<string, DataObjects.IISInfo?>> GetIISInfoAsync()
        {
            var result = new Dictionary<string, DataObjects.IISInfo?>();

            // Assume the JSON files are located in the content root (same folder as appsettings.json)
            string basePath = _env.ContentRootPath;
            string cmsPath = Path.Combine(basePath, "IISInfo_AzureCMS.json");
            string devPath = Path.Combine(basePath, "IISInfo_AzureDev.json");
            string prodPath = Path.Combine(basePath, "IISInfo_AzureProd.json");

            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            };

            // Use the ContentRootFileProvider to set up file watchers
            var fileProvider = _env.ContentRootFileProvider;

            // AzureCMS
            if (File.Exists(cmsPath)) {
                string cacheKey = "IISInfo_AzureCMS";
                if (!_cache.TryGetValue(cacheKey, out DataObjects.IISInfo? cmsData)) {
                    string json = await File.ReadAllTextAsync(cmsPath);
                    cmsData = JsonSerializer.Deserialize<DataObjects.IISInfo>(json, options);
                    // Create a cache entry that will expire when the file changes
                    var token = fileProvider.Watch("IISInfo_AzureCMS.json");
                    var cacheOptions = new MemoryCacheEntryOptions().AddExpirationToken(token);
                    _cache.Set(cacheKey, cmsData, cacheOptions);
                }
                result["AzureCMS"] = cmsData;
            }

            // AzureDev
            if (File.Exists(devPath)) {
                string cacheKey = "IISInfo_AzureDev";
                if (!_cache.TryGetValue(cacheKey, out DataObjects.IISInfo? devData)) {
                    string json = await File.ReadAllTextAsync(devPath);
                    devData = JsonSerializer.Deserialize<DataObjects.IISInfo>(json, options);
                    var token = fileProvider.Watch("IISInfo_AzureDev.json");
                    var cacheOptions = new MemoryCacheEntryOptions().AddExpirationToken(token);
                    _cache.Set(cacheKey, devData, cacheOptions);
                }
                result["AzureDev"] = devData;
            }

            // AzureProd
            if (File.Exists(prodPath)) {
                string cacheKey = "IISInfo_AzureProd";
                if (!_cache.TryGetValue(cacheKey, out DataObjects.IISInfo? prodData)) {
                    string json = await File.ReadAllTextAsync(prodPath);
                    prodData = JsonSerializer.Deserialize<DataObjects.IISInfo>(json, options);
                    var token = fileProvider.Watch("IISInfo_AzureProd.json");
                    var cacheOptions = new MemoryCacheEntryOptions().AddExpirationToken(token);
                    _cache.Set(cacheKey, prodData, cacheOptions);
                }
                result["AzureProd"] = prodData;
            }

            return result;
        }
    }
}