using Microsoft.Extensions.Caching.Memory;
using System.IO;

namespace CICD.Services
{
    public class FilesPrecacheService : BackgroundService
    {
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        public FilesPrecacheService(IMemoryCache cache, IConfiguration configuration)
        {
            _cache = cache;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Get the local files path from configuration (e.g. "LocalFilesPath")
            string localFilesPath = _configuration.GetValue<string>("LocalFilesPath");
            if (string.IsNullOrWhiteSpace(localFilesPath)) {
                // No local files path provided—skip precaching.
                return;
            }

            await PrecacheFiles(localFilesPath, stoppingToken);
            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);
                await PrecacheFiles(localFilesPath, stoppingToken);
            }
        }

        private Task PrecacheFiles(string path, CancellationToken cancellationToken)
        {
            try {
                if (!Directory.Exists(path)) {
                    Console.WriteLine($"Directory '{path}' does not exist. Skipping file precaching.");
                    return Task.CompletedTask;
                }

                var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Where(f => {
                        var segments = f.Split(Path.DirectorySeparatorChar);
                        bool inHiddenFolder = segments.Take(segments.Length - 1)
                            .Any(seg => seg.StartsWith("."));
                        if (inHiddenFolder)
                            return false;
                        bool binOrObj = segments.Any(seg =>
                            seg.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                            seg.Equals("obj", StringComparison.OrdinalIgnoreCase));
                        return !binOrObj;
                    })
                    .Select(f => new DataObjects.FileItem { FileName = Path.GetFileName(f), FullPath = f })
                    .ToList();

                string cacheKey = "LocalFilesCache";
                _cache.Set(cacheKey, files, TimeSpan.FromMinutes(5));
                Console.WriteLine($"Cached {files.Count} files from '{path}'.");
            } catch (Exception ex) {
                Console.WriteLine($"Error precaching files: {ex.Message}");
            }
            return Task.CompletedTask;
        }
    }
}
