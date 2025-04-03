namespace CICD;

public class ConfigurationHelper : IConfigurationHelper
{
    private ConfigurationHelperLoader _loader = new ConfigurationHelperLoader();

    public ConfigurationHelper(ConfigurationHelperLoader ConfigurationLoader)
    {
        _loader = ConfigurationLoader;
    }


    public string? PAT {
        get {
            return _loader.PAT;
        }
    }

    public string? ProjectId {
        get {
            return _loader.ProjectId;
        }
    }


    public string? RepoId {
        get {
            return _loader.RepoId;
        }
    }

    public string? Branch {
        get {
            return _loader.Branch;
        }
    }

    public string? OrgName {
        get {
            return _loader.OrgName;
        }
    }

    public string? BasePath {
        get {
            return _loader.BasePath;
        }
    }

    public ConfigurationHelperConnectionStrings ConnectionStrings {
        get {
            return _loader.ConnectionStrings;
        }
    }

    public List<string>? GloballyDisabledModules {
        get {
            return _loader.GloballyDisabledModules;
        }
    }
}

public interface IConfigurationHelper
{
    public string? PAT { get; }
    public string? ProjectId { get; }
    public string? RepoId { get; }
    public string? Branch { get; }
    public string? OrgName { get; }
    public string? BasePath { get; }
    ConfigurationHelperConnectionStrings ConnectionStrings { get; }
    List<string>? GloballyDisabledModules { get; }
}

public class ConfigurationHelperLoader
{
    public string? PAT { get; set; }
    public string? ProjectId { get; set; }
    public string? RepoId { get; set; }
    public string? Branch { get; set; }
    public string? OrgName { get; set; }
    public string? BasePath { get; set; }
    public ConfigurationHelperConnectionStrings ConnectionStrings { get; set; } = new ConfigurationHelperConnectionStrings();
    public List<string>? GloballyDisabledModules { get; set; }
}

public class ConfigurationHelperConnectionStrings
{
    public string? AppData { get; set; }
}