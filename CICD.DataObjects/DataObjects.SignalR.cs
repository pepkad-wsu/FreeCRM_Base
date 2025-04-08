namespace CICD;

public partial class DataObjects
{

    public enum SignalRUpdateType
    {
        File,
        Language,
        LastAccessTime,
        Setting,
        RegisterSignalR,
        Tenant,
        Unknown,
        User,
        UserAttendance,
        UserPreferences,
        LoadingDevOpsInfoStatusUpdate
    }
    public class SignalrClientRegistration
    {
        public string? RegistrationId { get; set; }
        public string? ConnectionId { get; set; }
        public string? GroupId { get; set; }
    }
    public class SignalRUpdate
    {
        public Guid? TenantId { get; set; }
        public Guid? ItemId { get; set; }
        public Guid? UserId { get; set; }

        #region direction connection variables - works without userid or tenantid.. unique to the browser
        public string? ConnetionId { get; set; } 
        public string? GroupId { get; set; }
        #endregion   

        public string? UserDisplayName { get; set; }
        public SignalRUpdateType UpdateType { get; set; }
        public string Message { get; set; } = "";
        public object? Object { get; set; }
        public string? ObjectAsString { get; set; }
    }
}