using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CICD.Server.Controllers;

// Use this file as a place to put any application-specific API endpoints.

public partial class DataController
{
    /// <summary>
    /// these are the project repo and branch were we are storing the yml files for release pipelines
    /// </summary>
    /// <returns></returns>
    private (string orgName, string pat, string projectId, string repoId, string branch) GetReleasePipelinesDevOpsConfig()
    {
        string orgName = configurationHelper?.OrgName ?? "";
        string pat = configurationHelper?.PAT ?? "";
        string projectId = configurationHelper?.ProjectId ?? "";
        string repoId = configurationHelper?.RepoId ?? "";
        string branch = configurationHelper?.Branch ?? "";
        return (orgName, pat, projectId, repoId, branch);
    }

    #region Git & Pipeline Endpoints (Authenticated)

    // POST: api/Data/CreateReleasePipeline
    [HttpPost($"~/{DataObjects.Endpoints.ReleasePipelines.CreatePipeline}")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<IActionResult> CreateReleasePipeline([FromBody] DataObjects.PipelineCreationRequest request)
    {
        var config = GetReleasePipelinesDevOpsConfig();
        if (string.IsNullOrWhiteSpace(config.pat))
            return BadRequest("PAT not configured");
        try {
            var createdPipeline = await da.CreatePipeline(request, config.projectId, config.repoId, config.branch, config.pat, config.orgName);
            return Ok(createdPipeline);
        } catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    // GET: api/Data/DevopsGetBranchCsProjFileList
    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetBranchCsProjFileList}")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<ActionResult<DataObjects.DevopsOrgInfo>> GetBranchCsProjFileList([FromQuery] string repoId, [FromQuery] string projectId, [FromQuery] string branchName)
    {
        var config = GetReleasePipelinesDevOpsConfig();
        var result = await da.GetGitFileList(projectId, repoId, branchName, config.pat, config.orgName);
        var output = result.Files.Where(o => o.FileType == "csproj").ToList();
        return Ok(output);
    }

    // GET: api/Data/DevopsGetWsuEitOrgInfo
    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetWsuEitOrgInfo}")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<ActionResult<DataObjects.DevopsOrgInfo>> GetWsuEitOrgInfo()
    {
        var config = GetReleasePipelinesDevOpsConfig();
        // Build a composite key using the endpoint identifier and the PAT
        string cacheKey = $"DevopsOrgInfo_{config.pat}";
        DataObjects.DevopsOrgInfo output;
        if (_cache.TryGetValue(cacheKey, out DataObjects.DevopsOrgInfo cachedResult) &&
            !string.IsNullOrWhiteSpace(cachedResult?.OrgName)) {
            output = cachedResult;
        } else {
            output = await da.GetDevopsOrgInfo(config.pat, config.orgName);
            _cache.Set(cacheKey, output, TimeSpan.FromMinutes(5));
        }
        return Ok(output);
    }

    // GET: api/Data/DevopsGetOrgInfoByPat
    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetOrgInfoByPat}")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.DevopsOrgInfo>> GetOrgInfoByPat([FromQuery] string orgName, [FromQuery] string pat)
    {
        // Build a composite key using the endpoint identifier and the PAT
        string cacheKey = $"DevopsOrgInfo_{pat}";
        DataObjects.DevopsOrgInfo output;
        if (_cache.TryGetValue(cacheKey, out DataObjects.DevopsOrgInfo cachedResult) &&
            !string.IsNullOrWhiteSpace(cachedResult?.OrgName)) {
            output = cachedResult;
        } else {
            output = await da.GetDevopsOrgInfo(pat, orgName);
            _cache.Set(cacheKey, output, TimeSpan.FromMinutes(5));
        }
        return Ok(output);
    }

    // GET: api/Data/GetReleasePipelinesYmlFileContent
    [HttpGet($"~/{DataObjects.Endpoints.ReleasePipelines.GetYmlFileContent}")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<IActionResult> GetReleasePipelinesYmlFileContent([FromQuery] string filePath)
    {
        if (!filePath.ToLower().EndsWith(".yml")) {
            return BadRequest("Not a yml file, dont use this endpoint for this type of request");
        }
        var config = GetReleasePipelinesDevOpsConfig();
        if (string.IsNullOrWhiteSpace(config.pat))
            return BadRequest("PAT not configured");
        try {
            var content = await da.GetGitFileContent(config.projectId, config.repoId, config.branch, filePath, config.pat, config.orgName);
            return Ok(content);
        } catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost($"~/{DataObjects.Endpoints.ReleasePipelines.CreateVariableGroup}")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<IActionResult> CreateDevopsVariableGroup([FromBody] DataObjects.DevopsVariableGroup newGroup)
    {
        var config = GetReleasePipelinesDevOpsConfig();
        if (newGroup.Variables == null || newGroup.Variables.Count == 0)
            newGroup.Variables = [new DataObjects.DevopsVariable() { Name = "test", Value = "test", IsReadOnly = false, IsSecret = false }];
        try {
            var createdGroup = await da.CreateVariableGroup(config.projectId, config.pat, config.orgName, newGroup);
            return Ok(createdGroup);
        } catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost($"~/{DataObjects.Endpoints.ReleasePipelines.UpdateVariableGroup}")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<IActionResult> UpdateDevopsVariableGroup([FromBody] DataObjects.DevopsVariableGroup updatedGroup)
    {
        var config = GetReleasePipelinesDevOpsConfig();
        try {
            var result = await da.UpdateVariableGroup(config.projectId, config.pat, config.orgName, updatedGroup);
            return Ok(result);
        } catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }



    // GET: api/Data/DevopsGetIISInfo
    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetIISInfo}")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<IActionResult> GetIISInfo()
    {
        Dictionary<string, DataObjects.IISInfo?> iisData = await _iisInfoProvider.GetIISInfoAsync();
        return Ok(iisData);
    }

    // GET: api/Data/GetReleasePipelines
    [HttpGet($"~/{DataObjects.Endpoints.ReleasePipelines.GetPipelines}")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<IActionResult> GetReleasePipelines()
    {
        var config = GetReleasePipelinesDevOpsConfig();
        if (string.IsNullOrWhiteSpace(config.pat))
            return BadRequest("PAT not configured");
        try {
            var pipelines = await da.GetPipelines(config.projectId, config.pat, config.orgName);
            return Ok(pipelines);
        } catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }


    // POST: api/Data/UpdateGitFileContent
    [HttpPost("~/api/Data/UpdateReleasePipelineYmlFile")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<IActionResult> UpdateReleasePipelineYmlFile([FromQuery] string filePath, [FromBody] string fileContent)
    {
        var config = GetReleasePipelinesDevOpsConfig();
        if (string.IsNullOrWhiteSpace(config.pat))
            return BadRequest("PAT not configured");
        try {
            var result = await da.UpdateGitFileContent(config.projectId, config.repoId, config.branch, filePath, fileContent, config.pat, config.orgName);
            return Ok(result);
        } catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    // PUT: api/Data/UpdatePipeline
    [HttpPut("~/api/Data/UpdatePipeline")]
    [Authorize(Policy = Policies.AppAdmin)]
    public async Task<IActionResult> UpdatePipeline([FromBody] DataObjects.BuildDefinition pipelineDefinition)
    {
        var config = GetReleasePipelinesDevOpsConfig();
        if (string.IsNullOrWhiteSpace(config.pat))
            return BadRequest("PAT not configured");
        try {
            var updatedPipeline = await da.UpdatePipeline(pipelineDefinition, config.projectId, config.pat, config.orgName);
            return Ok(updatedPipeline);
        } catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }
    #endregion Git & Pipeline Endpoints
}
