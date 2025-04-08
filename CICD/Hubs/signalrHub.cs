using CICD.Client;
using Microsoft.AspNetCore.SignalR;
using System.Linq;
using System.Text.Json;
using static CICD.DataObjects;
using static Microsoft.Azure.Pipelines.WebApi.PipelinesResources;

namespace CICD.Server.Hubs
{
    public class CICDhub : Hub
    {
        private List<string> tenants = new();
        private List<string> allClientFingerprints = new();

        public async Task RegisterClient(string fingerprint)
        {
            if (!allClientFingerprints.Contains(fingerprint)) {
                allClientFingerprints.Add(fingerprint);
            }
            // Before adding a user to a Tenant group remove them from any groups they were in before.
            if (allClientFingerprints != null && allClientFingerprints.Count() > 0) {
                foreach (var clientfingerprint in allClientFingerprints) {
                    try {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, clientfingerprint);
                    } catch { }
                }
            }

            var clientRegistration = new DataObjects.SignalrClientRegistration { RegistrationId = Guid.NewGuid().ToString().Replace("-",""), ConnectionId = Context.ConnectionId, GroupId = fingerprint };

            await Groups.AddToGroupAsync(Context.ConnectionId, $"{fingerprint}_{clientRegistration.RegistrationId}");

            await Clients.Caller.SendAsync("SignalRUpdate", new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.RegisterSignalR,
                Message = "Registered with the fingerprint as groupid",
                ObjectAsString = Helpers.SerializeObject(clientRegistration),
            });
        }

        public async Task JoinTenantId(string TenantId)
        {
            if (!tenants.Contains(TenantId)) {
                tenants.Add(TenantId);
            }

            // Before adding a user to a Tenant group remove them from any groups they were in before.
            if (tenants != null && tenants.Count() > 0) {
                foreach (var tenant in tenants) {
                    try {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenant);
                    } catch { }
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, TenantId);
        }
    }
}
