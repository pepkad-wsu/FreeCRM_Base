using Microsoft.AspNetCore.Mvc;

namespace CICD.Server.Controllers
{
    [ApiController]
    public class SetupController : ControllerBase
    {
        private IConfiguration _config;
        private IDataAccess da;
        private IHostApplicationLifetime _hostLifetime;

        public SetupController(IDataAccess daInjection, IConfiguration config, IHostApplicationLifetime hostLifetime)
        {
            _config = config;
            da = daInjection;
            _hostLifetime = hostLifetime;
        }

        [HttpPost]
        [Route("~/api/Setup/SaveConnectionString")]
        public ActionResult<DataObjects.BooleanResponse> SaveConnectionString(DataObjects.ConnectionStringConfig csConfig)
        {
            DataObjects.BooleanResponse output = new DataObjects.BooleanResponse();

            //Try and save the connection string data and redirect to the Index page.
            string cs = String.Empty;

            if (csConfig != null) {
                
                if (!String.IsNullOrWhiteSpace(csConfig.SqlServer_Server) &&
                    !String.IsNullOrWhiteSpace(csConfig.SqlServer_Database)) {
                    cs = "Data Source=" + csConfig.SqlServer_Server +
                        ";Initial Catalog=" + csConfig.SqlServer_Database + ";";
                    if (csConfig.SqlServer_IntegratedSecurity) {
                        cs += "Integrated Security=true;";
                    }
                    if (csConfig.SqlServer_PersistSecurityInfo) {
                        cs += "Persist Security Info=True;";
                    }
                    if (csConfig.SqlServer_TrustServerCertificate) {
                        cs += "TrustServerCertificate=True;";
                    }
                    if (!String.IsNullOrWhiteSpace(csConfig.SqlServer_UserId)) {
                        cs += "User ID=" + csConfig.SqlServer_UserId + ";";
                    }
                    if (!String.IsNullOrWhiteSpace(csConfig.SqlServer_Password)) {
                        cs += "Password=" + csConfig.SqlServer_Password + ";";
                    }
                    cs += "MultipleActiveResultSets=True;";
                }

                if (!String.IsNullOrEmpty(cs)) {
                    var appSettingsPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "appsettings.json");
                    var json = da.StringValue(System.IO.File.ReadAllText(appSettingsPath));

                    var jsonObject = System.Text.Json.Nodes.JsonNode.Parse(json, new System.Text.Json.Nodes.JsonNodeOptions { PropertyNameCaseInsensitive = true });
                    if (jsonObject != null) {
                        System.Text.Json.Nodes.JsonObject obj = jsonObject.AsObject();

                        var csAppData = obj["ConnectionStrings"]?["AppData"];
                        if (csAppData != null) {
                            csAppData.ReplaceWith<string>(cs);
                        }

                        json = System.Text.Json.JsonSerializer.Serialize(jsonObject, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    }


                    System.IO.File.WriteAllText(appSettingsPath, json);

                    _hostLifetime.StopApplication();

                    output.Result = true;
                } else {
                    output.Messages.Add("Unable to build connection string. Please make sure all required fields are completed.");
                }
            } else {
                output.Messages.Add("Missing Config Object");
            }

            return Ok(output);
        }
    }
}