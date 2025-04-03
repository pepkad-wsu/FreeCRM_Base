using CICD.Components;
using CICD.Server.Controllers;
using CICD.Server.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using System.Security.Claims;

namespace CICD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            var isDevelopment = builder.Environment.IsDevelopment();
            if (!isDevelopment) {
                
            }

            // Attempts to read the AzureSignalRurl setting from appsettings.json.
            string azureSignalRUrl = String.Empty + builder.Configuration.GetValue<string>("AzureSignalRurl");
            if (String.IsNullOrWhiteSpace(azureSignalRUrl)) {
                // Fall back to local SignalR when the setting isn't specified.
                builder.Services.AddSignalR();
            } else {
                // Include the parameter to tell Azure SignalR what endpoint to use.
                builder.Services.AddSignalR().AddAzureSignalR("Endpoint=" + azureSignalRUrl);
            }

            builder.Services.AddRazorComponents()
                .AddInteractiveWebAssemblyComponents();

            builder.Services.AddRazorPages();
            builder.Services.AddHttpContextAccessor();

           

            builder.Services.AddSingleton<IServiceProvider>(provider => provider);

            string _localModeUrl = String.Empty + builder.Configuration.GetValue<string>("LocalModeUrl");
            string _connectionString = String.Empty + builder.Configuration.GetConnectionString("AppData");
            builder.Services.AddTransient<IDataAccess>(x => ActivatorUtilities.CreateInstance<DataAccess>(x, _connectionString, _localModeUrl, x.GetRequiredService<IServiceProvider>()));

            var useAuthorization = CustomAuthenticationProviders.UseAuthorization(builder);
            builder.Services.AddTransient<ICustomAuthentication>(x => ActivatorUtilities.CreateInstance<CustomAuthentication>(x, useAuthorization));

            var allowApplicationEmbedding = builder.Configuration.GetValue<bool>("AllowApplicationEmbedding");
            if (allowApplicationEmbedding) {
                builder.Services.AddAntiforgery(x => x.SuppressXFrameOptionsHeader = true);
            }

            // Create DI for supported configuration items.
            var basePath = builder.Configuration.GetValue<string>("BasePath");
            if (!String.IsNullOrWhiteSpace(basePath) && !basePath.EndsWith("/")) {
                basePath += "/";
            }

            List<string> disabled = new List<string>();
            var globallyDisabledModules = builder.Configuration.GetSection("GloballyDisabledModules").GetChildren();
            if(globallyDisabledModules != null && globallyDisabledModules.Any()) {
                foreach(var item in globallyDisabledModules.ToArray().Select(c => c.Value).ToList()) {
                    if (!String.IsNullOrWhiteSpace(item)) {
                        disabled.Add(item.ToLower());
                    }
                }
            }

            var configurationHelperLoader = new ConfigurationHelperLoader {
                BasePath = basePath,
                ConnectionStrings = new ConfigurationHelperConnectionStrings {
                    AppData = builder.Configuration.GetConnectionString("AppData"),
                },
                GloballyDisabledModules = disabled,
            };
            builder.Services.AddTransient<IConfigurationHelper>(x => ActivatorUtilities.CreateInstance<ConfigurationHelper>(x, configurationHelperLoader));

            builder.Services.AddAuthorization(options => {
                options.AddPolicy("AppAdmin", policy => policy.RequireClaim(ClaimTypes.Role, "AppAdmin"));
                options.AddPolicy("Admin", policy => policy.RequireClaim(ClaimTypes.Role, "Admin"));
                options.AddPolicy("ManageFiles", policy => policy.RequireClaim(ClaimTypes.Role, "ManageFiles"));
                options.AddPolicy("PreventPasswordChange", policy => policy.RequireClaim(ClaimTypes.Role, "PreventPasswordChange"));
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment()) {
                app.UseWebAssemblyDebugging();
            } else {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            
            //app.UseStaticFiles(); Replaced by the newer MapStaticAssets middleware.
            app.MapStaticAssets();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseAntiforgery();

            app.MapHub<CICDhub>("/CICDhub", signalRConnctionOptions => {
                signalRConnctionOptions.AllowStatefulReconnects = true;
            });

            app.MapRazorPages();

            app.MapControllers();

            app.MapRazorComponents<App>()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(CICD.Client.Pages.Index).Assembly);

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            app.Run();
        }
    }
}
