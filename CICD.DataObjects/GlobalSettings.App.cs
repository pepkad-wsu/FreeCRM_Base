namespace CICD;

/// <summary>
/// This static class is used to persist a few settings about the startup state of the application.
/// </summary>
public static partial class GlobalSettings
{
    public class EnvironmentOptions
    {
        public string AgentPool { get; set; } = "Default";
        public string Hostname { get; set; }
        public bool IsDevelopment { get; set; }        
        public string IISJsonFilePath { get; set; }
    }
    public static class App {
        public static string Name { get; set; } = "CICD";
        public static string Version { get; set; } = "1.0.0";
        public static string ReleaseDate { get; set; } = "3/6/2025";
        public static string CompanyName { get; set; } = "Company Name";
        public static string CompanyUrl { get; set; } = "em.wsu.edu";

        public static Dictionary<string, EnvironmentOptions> EnvironmentOptions = new Dictionary<string, EnvironmentOptions>() {
            { "DEV",  new (){AgentPool ="AzureDev", Hostname = $"dev.{CompanyUrl}", IISJsonFilePath = "" } },
            { "PROD",   new (){AgentPool ="AzureProd",Hostname =  $"prod.{CompanyUrl}", IISJsonFilePath = "" } },
            { "CRM", new (){AgentPool ="AzureCRM",Hostname =  $"crm.{CompanyUrl}", IISJsonFilePath = "" } },
        };

        // anything starting with . _ or XX - OLD - will be ignored.  some common methods of indicating private things so might as well follow it
        public static List<string> AzureDevOpsProjectNameStartsWithIgnoreValues = ["XX - OLD - ",".", "_"];

        public static string IISJsonFilePathDefault= "IISInfo";
        
        public static string VariableGroupNameDefault = "AppSettings";

        public static string[] AnonamousAccessList = ["ABOUT", "DATABASEOFFLINE", "LOGIN", "LOGOUT", "PROCESSLOGIN", "SETUP", "DEVOPS", "", "HOME"];
    }
}