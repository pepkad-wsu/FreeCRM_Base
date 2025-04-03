using Microsoft.Extensions.Caching.Memory;

namespace CICD.Services
{
    public class DevopsPrecacheService : BackgroundService
    {
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly IDataAccess _dataAccess;
        public DevopsPrecacheService(IDataAccess dataAccess, IMemoryCache cache, IConfiguration configuration)
        {
            _cache = cache;
            _configuration = configuration;
            _dataAccess = dataAccess;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var pat = _configuration.GetValue<string>("DevOpsPAT");
            var orgName = _configuration.GetValue<string>("DevOpsOrgName");
            var projectId = _configuration.GetValue<string>("DevOpsProjectId");
            if (string.IsNullOrWhiteSpace(pat)) {
                // No PAT provided—skip precaching.
                return;
            }
            await PrecacheDevopsInfo(pat, orgName, projectId, stoppingToken);
            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);
                await PrecacheDevopsInfo(pat,orgName, projectId, stoppingToken);
            }
        }

        private async Task PrecacheDevopsInfo(string pat,string orgName,string variablesProjectId, CancellationToken cancellationToken)
        {
            try {
                var devopsInfo = await _dataAccess.GetDevopsOrgInfo(pat, orgName, variablesProjectId);
                string cacheKey = "DevopsOrgInfo";
                _cache.Set(cacheKey, devopsInfo, TimeSpan.FromMinutes(5));
            } catch (Exception ex) {
                Console.WriteLine($"Error pre-caching DevOps info: {ex.Message}");
            }
        }
    }
}