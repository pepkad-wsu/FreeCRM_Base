using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace CICD;

public partial class DataAccess: IDisposable, IDataAccess
{
    private int _accountLockoutMaxAttempts = 5;
    private int _accountLockoutMinutes = 10;
    private string _appName = $"{GlobalSettings.App.Name}";
    private DataObjects.AuthenticationProviders? _authenticationProviders;
    private string _connectionString;
    private string _copyright = $"{GlobalSettings.App.CompanyName}";
    private EFDataModel data;
    private bool _firstInit = true;
    private Guid _guid1 = new Guid("00000000-0000-0000-0000-000000000001");
    private Guid _guid2 = new Guid("00000000-0000-0000-0000-000000000002");
    private Microsoft.AspNetCore.Http.HttpContext? _httpContext;
    private HttpRequest? _httpRequest;
    private HttpResponse? _httpResponse;
    private string _localModeUrl = "";
    private bool _open;
    private DateOnly _released = DateOnly.FromDateTime(Convert.ToDateTime($"{GlobalSettings.App.ReleaseDate}"));
    private IServiceProvider? _serviceProvider;
    private string _uniqueId = Guid.NewGuid().ToString().Replace("-", "").ToLower();

    /// <summary>
    /// The migrations engine is one of the last components I will work on after getting the primary data structure defined.
    /// </summary>
    private bool _useMigrations = false;
    private string _version = $"{GlobalSettings.App.Version}";
    private readonly IMemoryCache _cache;

    public DataAccess(string ConnectionString = "",  string LocalModeUrl = "", IServiceProvider? serviceProvider = null, IMemoryCache memoryCache = null)
    {   
        // Use the injected memoryCache if provided; otherwise create a new one.
        _cache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());

        _connectionString = ConnectionString;
        _localModeUrl = LocalModeUrl;
        _serviceProvider = serviceProvider;

        if (!String.IsNullOrWhiteSpace(_connectionString)) {
            _connectionString = ConnectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<EFDataModel>();

        // Both the Connection String and Database Type parameters are required.
        // Otherwise the app will redirect to the page to configure the database connection.
        if (!String.IsNullOrEmpty(_connectionString) ) {
            optionsBuilder.UseSqlServer(_connectionString, options => options.EnableRetryOnFailure());
            data = new EFDataModel(optionsBuilder.Options);

            if (!GlobalSettings.StartupRun && _firstInit) {
                _firstInit = false;

                if (data.Database.CanConnect()) {
                    _open = true;

                    // See if any migrations need to be applied.
                    if (_useMigrations) {
                        DatabaseApplyLatestMigrations();
                    }
                } else {
                    // Try and create the database using the built-in EF command
                    try {
                        data.Database.EnsureCreated();

                        // See if any migrations need to be applied.
                        if (data.Database.CanConnect()) {
                            if (_useMigrations) {
                                DatabaseApplyLatestMigrations();
                            }

                            _open = true;
                        } else {
                            //throw new Exception("Unable to connect to the database. Please check your connection string.");
                        }
                    } catch (Exception ex) {
                        // This would indicate that the database is not configured correctly, or that
                        // for some reason it is offline.
                        GlobalSettings.StartupErrorMessages = new List<string>();

                        GlobalSettings.StartupError = true;
                        GlobalSettings.StartupErrorCode = "DatabaseOffline";
                        GlobalSettings.StartupErrorMessages.Add(ex.Message);
                        if (ex.InnerException != null && !String.IsNullOrEmpty(ex.InnerException.Message)) {
                            GlobalSettings.StartupErrorMessages.Add(ex.InnerException.Message);
                        }
                        return;
                    }
                }

                // Make sure the default data exists and is up to date.
                SeedTestData();

                GlobalSettings.StartupRun = true;
                GlobalSettings.RunningSince = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            } else {
                _open = true;
            }

        } else {
            // To prevent errors just use an InMemory copy
            optionsBuilder.UseInMemoryDatabase("InMemory");
            data = new EFDataModel(optionsBuilder.Options);

            if (!GlobalSettings.StartupRun) {
                GlobalSettings.StartupError = true;
                GlobalSettings.StartupErrorCode = "MissingConnectionString";
            } else {
                throw new NullReferenceException("Missing Connection String and/or Database Type");
            }
        }
    }
}