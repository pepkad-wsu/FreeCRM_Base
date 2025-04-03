// =============================================================================
// DataAccess.DevOps.cs
// =============================================================================
// This file defines the data access layer for Azure DevOps operations.
// It contains the IDataAccess interface and its partial implementation,
// grouped into regions by functionality.
// =============================================================================

using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace CICD
{
    public partial interface IDataAccess
    {
        // Git File operations
        Task<DataObjects.GitUpdateResult> CreateGitFile(string projectId, string repoId, string branch, string filePath, string fileContent, string pat, string orgName);
        // Pipeline operations
        Task<DataObjects.BuildDefinition> CreatePipeline(DataObjects.PipelineCreationRequest request, string projectId, string repoId, string branch, string pat, string orgName);

        Task<DataObjects.GitUpdateResult> DeleteGitFile(string projectId, string repoId, string branch, string filePath, string pat, string orgName);
        Task DeletePipeline(int pipelineId, string projectId, string pat, string orgName);

        Task<DataObjects.GitUpdateResult> EditGitFile(string projectId, string repoId, string branch, string filePath, string fileContent, string pat, string orgName);
        // Organization Operations
        Task<DataObjects.DevopsOrgInfo> GetDevopsOrgInfo(string pat, string orgName, string variablesProjectId);
        Task<DataObjects.DevopsVariableGroup> CreateVariableGroup(string projectId, string pat, string orgName, DataObjects.DevopsVariableGroup newGroup);
        Task<DataObjects.DevopsVariableGroup> UpdateVariableGroup(string projectId, string pat, string orgName, DataObjects.DevopsVariableGroup updatedGroup);
        Task<string> GetGitFileContent(string projectId, string repoId, string branch, string filePath, string pat, string orgName);
        // NEW: Include the GetGitFileLineCount signature
        Task<(int lineCount, int charCount, bool fromCache)> GetGitFileLineCount(string projectId, string repoId, string branch, string filePath, string pat, string orgName);

        Task<DataObjects.DevopsFileStructure> GetGitFileList(string projectId, string repoId, string branch, string pat, string orgName);
        Task<DataObjects.BuildDefinition> GetPipeline(int pipelineId, string projectId, string pat, string orgName);

        Task<List<DataObjects.DevOpsBuild>> GetPipelineRuns(int pipelineId, string projectId, string pat, string orgName, int skip = 0, int top = 10);

        Task<List<DataObjects.DevopsPipelineDefinition>> GetPipelines(string projectId, string pat, string orgName);

        Task<DataObjects.GitUpdateResult> UpdateGitFileContent(string projectId, string repoId, string branch, string filePath, string fileContent, string pat, string orgName);
        Task<DataObjects.BuildDefinition> UpdatePipeline(DataObjects.BuildDefinition definition, string projectId, string pat, string orgName);
    }

    public partial class DataAccess
    {
        // Modified CreateConnection accepts an optional org parameter.
        private VssConnection CreateConnection(string pat, string orgName)
        {
            var collectionUri = new Uri($"https://dev.azure.com/{orgName}");
            var credentials = new VssBasicCredential(string.Empty, pat);
            return new VssConnection(collectionUri, credentials);
        }

        #region Organization Operations
        public async Task<DataObjects.DevopsOrgInfo> GetDevopsOrgInfo(string pat, string orgName, string variablesProjectId)
        {
            var output = new DataObjects.DevopsOrgInfo {
                OrgName = orgName ?? "wsueit",
                Projects = new List<DataObjects.DevopsProjectInfo>()
            };

            using (var connection = CreateConnection(pat, orgName)) {
                try {
                    var projectClient = connection.GetClient<ProjectHttpClient>();
                    List<TeamProjectReference> projects = new List<TeamProjectReference>();
                    try {
                        projects = (await projectClient.GetProjects()).ToList();
                        Console.WriteLine($"Found {projects.Count} projects in organization '{output.OrgName}':");
                    } catch (Exception ex) {
                        Console.WriteLine($"Error fetching projects for organization '{output.OrgName}': {ex.Message}");
                    }

                    var projectTasks = projects.Select(async project => {
                        var projInfo = new DataObjects.DevopsProjectInfo {
                            ProjectName = project.Name,
                            ProjectId = project.Id.ToString(),
                            CreationDate = project.LastUpdateTime,
                            GitRepos = new List<DataObjects.DevopsGitRepoInfo>(),

                        };

                        // ok lets get the project... the get list isn't returning links
                        var p = await projectClient.GetProject(project.Id.ToString());
                        dynamic projectResource = p.Links.Links["web"];
                        // this needs to be web encoded
                        // 
                        // TODO:  If starts with xx just continue;
                        //
                        projInfo.ResourceUrl = string.Empty + projectResource.Href;

                        try {
                            var gitClient = connection.GetClient<GitHttpClient>();
                            var gitRepos = await gitClient.GetRepositoriesAsync(project.Id);

                            if (gitRepos.Count > 0) {
                                Console.WriteLine("  Git Repositories:");
                                var repoTasks = gitRepos.Select(async repo => {
                                    var repoInfo = new DataObjects.DevopsGitRepoInfo {
                                        RepoName = repo.Name,
                                        RepoId = repo.Id.ToString(),
                                        GitBranches = new List<DataObjects.DevopsGitRepoBranchInfo>()
                                    };

                                    var r = await gitClient.GetRepositoryAsync(project.Id, repo.Id);
                                    dynamic repoResource = r.Links.Links["web"];
                                    repoInfo.ResourceUrl = repoResource.Href;

                                    Console.WriteLine($"    - {repo.Name}");

                                    try {
                                        var branches = await gitClient.GetBranchesAsync(project.Id, repo.Id);
                                        if (branches != null && branches.Any()) {
                                            foreach (var branch in branches) {
                                                try {
                                                    var branchInfo = new DataObjects.DevopsGitRepoBranchInfo {
                                                        BranchName = branch.Name,
                                                        LastCommitDate = branch?.Commit?.Committer?.Date
                                                    };

                                                    // Remove any "refs/heads/" prefix for a cleaner branch name
                                                    var branchDisplayName = branch.Name.Replace("refs/heads/", "");

                                                    // Use the repository's web URL as the base and append the branch version query
                                                    branchInfo.ResourceUrl = $"{repoInfo.ResourceUrl}?version=GB{Uri.EscapeDataString(branchDisplayName)}";

                                                    try {
                                                        var versionDescriptor = new GitVersionDescriptor {
                                                            Version = branch.Name,
                                                            VersionType = GitVersionType.Branch
                                                        };

                                                        var items = await gitClient.GetItemsAsync(
                                                            project: project.Id.ToString(),
                                                            repositoryId: repo.Id.ToString(),
                                                            scopePath: null,
                                                            recursionLevel: VersionControlRecursionType.Full,
                                                            includeLinks: true,
                                                            versionDescriptor: versionDescriptor);

                                                        var fileStructure = new DataObjects.DevopsFileStructure();
                                                        if (items != null && items.Any()) {
                                                            foreach (var item in items) {
                                                                if (!item.IsFolder) {
                                                                    var resourceUrl = string.Empty;
                                                                    string marker = "/items//";
                                                                    var url = item.Url;
                                                                    int markerIndex = url.IndexOf(marker);
                                                                    if (markerIndex >= 0) {
                                                                        // Include the marker in the left part
                                                                        string rightPart = url.Substring(markerIndex + marker.Length);
                                                                        var path = rightPart.Split("?")[0];
                                                                        // example path so we should take the org name then project name then repo name then the path.. looks like we can ignore branch?
                                                                        // https://dev.azure.com/wsueit/Pipelines/_git/Pipelines2?path=/CICD.Client/wwwroot/appsettings.json
                                                                        // or https://dev.azure.com/wsueit/Athletic%20Eligibility%202/_git/Athletic%20Eligibility%202?version=GBmaster&path=/AthleticEligibility.DataObjects/DataObjects.App.cs
                                                                        // looks like they get escaped... which is probably fine i bet its being escaped already
                                                                        //https://dev.azure.com/wsueit/Pipelines/_git/Pipelines2?version=GBmaster&path=/CICD.sln

                                                                        // Use the repository's web URL as the base and append the branch version query
                                                                        // branchInfo.ResourceUrl = $"{repoInfo.ResourceUrl}?version=GB{Uri.EscapeDataString(branchDisplayName)}";
                                                                        resourceUrl = $"{branchInfo.ResourceUrl}&path=/" + path;
                                                                    }

                                                                    var fileItem = new DataObjects.DevopsFileItem {
                                                                        Path = item.Path,
                                                                        FileType = Path.GetExtension(item.Path),
                                                                        ResourceUrl = resourceUrl
                                                                    };
                                                                    fileStructure.Files.Add(fileItem);
                                                                }
                                                            }
                                                        }
                                                        branchInfo.FileStructure = fileStructure;
                                                    } catch (Exception exFile) {
                                                        Console.WriteLine($"Error fetching file structure for branch '{branch.Name}' in repo '{repo.Name}': {exFile.Message}");
                                                    }

                                                    repoInfo.GitBranches.Add(branchInfo);
                                                } catch (Exception ex) {
                                                    Console.WriteLine($"Error processing branch '{branch?.Name}' in repo '{repo.Name}': {ex.Message}");
                                                }
                                            }
                                        }
                                    } catch (Exception ex) {
                                        Console.WriteLine($"Error fetching branches for repo '{repo.Name}': {ex.Message}");
                                    }

                                    return repoInfo;
                                });

                                var repos = await Task.WhenAll(repoTasks);
                                projInfo.GitRepos.AddRange(repos);
                            } else {
                                Console.WriteLine("  No Git repositories found.");
                            }
                        } catch (Exception ex) {
                            Console.WriteLine($"Error fetching Git repositories for project '{project.Name}': {ex.Message}");
                        }

                        return projInfo;
                    });

                    var projectInfos = await Task.WhenAll(projectTasks);
                    output.Projects.AddRange(projectInfos);
                } catch (Exception ex) {
                    Console.WriteLine($"Error during DevOps connection processing: {ex.Message}");
                }
            }

            // ok so now we need to attach the variable group info
            //List<DataObjects.DevopsVariableGroup>
            output.DevopsVariableGroups = await GetProjectVariableGroupsAsync(pat, orgName, variablesProjectId);

            Console.WriteLine("Done.");
            output.CreationDate = DateTime.UtcNow;
            return output;
        }
        public async Task<DataObjects.DevopsVariableGroup> UpdateVariableGroup(string projectId, string pat, string orgName, DataObjects.DevopsVariableGroup updatedGroup)
        {
            using (var connection = CreateConnection(pat, orgName)) {
                var taskAgentClient = connection.GetClient<TaskAgentHttpClient>();
                var projectClient = connection.GetClient<ProjectHttpClient>();


                TeamProjectReference project = await projectClient.GetProject(projectId);

                // get all the groups by its project id
                var devopsVariableGroups = await taskAgentClient.GetVariableGroupsAsync(new Guid(projectId));
                // now look for the group by its id
                var group = devopsVariableGroups.FirstOrDefault(g => g.Id == updatedGroup.Id);

                // Prepare the parameters from the updated group.
                var parameters = new VariableGroupParameters {

                    Name = updatedGroup.Name,
                    Description = updatedGroup.Description,
                    Type = "Vsts", // adjust if needed
                    Variables = updatedGroup.Variables.ToDictionary(
                        kv => kv.Name,
                        kv => new VariableValue {
                            Value = kv.Value,
                            IsSecret = kv.IsSecret,
                            IsReadOnly = kv.IsReadOnly
                        },
                        StringComparer.OrdinalIgnoreCase),
                    VariableGroupProjectReferences = [new VariableGroupProjectReference {
                            Name = project.Name,
                            Description = project.Description, ProjectReference = new ProjectReference {
                                Id = project.Id,
                                Name = project.Name
                            }
                        }]
                };

                try {
                    // Call the update API on Azure DevOps.
                    var updatedVariableGroup = await taskAgentClient.UpdateVariableGroupAsync(group.Id, parameters, cancellationToken: CancellationToken.None);
                    // Map the returned object to your DataObjects model.
                    var mappedGroup = new DataObjects.DevopsVariableGroup {
                        Id = updatedVariableGroup.Id,
                        Name = updatedVariableGroup.Name,
                        Description = updatedVariableGroup.Description,
                        Variables = updatedVariableGroup.Variables
                            .ToDictionary(kvp => kvp.Key, kvp => new DataObjects.DevopsVariable {
                                Name = kvp.Key,
                                Value = kvp.Value.Value,
                                IsSecret = kvp.Value.IsSecret,
                                IsReadOnly = kvp.Value.IsReadOnly
                            }).Values.ToList(),
                        ResourceUrl = string.Empty
                    };
                    return mappedGroup;
                } catch (Exception ex) {
                    throw new Exception("Error updating variable group: " + ex.Message);
                }
            }
        }
        public async Task<DataObjects.DevopsVariableGroup> CreateVariableGroup(string projectId, string pat, string orgName, DataObjects.DevopsVariableGroup newGroup)
        {
            using (var connection = CreateConnection(pat, orgName)) {
                var taskAgentClient = connection.GetClient<TaskAgentHttpClient>();
                var projectClient = connection.GetClient<ProjectHttpClient>();

                try {

                    TeamProjectReference project = await projectClient.GetProject(projectId);

                    var parameters = new VariableGroupParameters {
                        Name = newGroup.Name,
                        Description = newGroup.Description,
                        Type = "Vsts", // Common type for variable groups.
                        Variables = newGroup.Variables.ToDictionary(
                            kv => kv.Name,
                            kv => new VariableValue {
                                Value = kv.Value,
                                IsSecret = kv.IsSecret,
                                IsReadOnly = kv.IsReadOnly
                            },
                            StringComparer.OrdinalIgnoreCase),
                        VariableGroupProjectReferences = [new VariableGroupProjectReference {
                            Name = project.Name,
                            Description = project.Description, ProjectReference = new ProjectReference {
                                Id = project.Id,
                                Name = project.Name
                            }
                        }]
                    };

                    // Use the overload that accepts a project ID so the group is created for that project.
                    var createdGroup = await taskAgentClient.AddVariableGroupAsync(parameters, new Guid(projectId), cancellationToken: CancellationToken.None);

                    var mappedGroup = new DataObjects.DevopsVariableGroup {
                        Id = createdGroup.Id,
                        Name = createdGroup.Name,
                        Description = createdGroup.Description,
                        Variables = createdGroup.Variables.ToDictionary(
                            kv => kv.Key,
                            kv => new DataObjects.DevopsVariable {
                                Name = kv.Key,
                                Value = kv.Value.Value,
                                IsSecret = kv.Value.IsSecret,
                                IsReadOnly = kv.Value.IsReadOnly
                            }).Values.ToList(),
                        ResourceUrl = string.Empty
                    };

                    return mappedGroup;
                } catch (Exception ex) {
                    throw new Exception("Error creating variable group: " + ex.Message);
                }
            }
        }

        private async Task<List<DataObjects.DevopsVariableGroup>> GetProjectVariableGroupsAsync(string pat, string orgName, string projectId)
        {
            // Create a connection to your Azure DevOps organization
            var connection = CreateConnection(pat, orgName);

            var variableGroups = new List<DataObjects.DevopsVariableGroup>();

            try {
                // Get the TaskAgentHttpClient which provides access to variable groups, among other resources
                var taskAgentClient = connection.GetClient<TaskAgentHttpClient>();


                var projectClient = connection.GetClient<ProjectHttpClient>();

                var project = await projectClient.GetProject(projectId);
                dynamic projectResource = project.Links.Links["web"];
                var projectUrl = Uri.EscapeUriString(string.Empty + projectResource.Href);

                // Retrieve all variable groups for the specified project.
                // Optional parameters can be provided if you want to filter or include secrets.
                var devopsVariableGroups = await taskAgentClient.GetVariableGroupsAsync(project.Id);

                variableGroups = devopsVariableGroups.Select(g => {
                    var group = taskAgentClient.GetVariableGroupAsync(project.Id, g.Id).Result;

                    var vargroup = new DataObjects.DevopsVariableGroup {
                        Id = g.Id,
                        Name = g.Name,
                        Description = g.Description,
                        ResourceUrl = $"{projectUrl}/_library?itemType=VariableGroups&view=VariableGroupView&variableGroupId={g.Id}",
                        Variables = g.Variables.Select(v => new DataObjects.DevopsVariable {
                            Name = v.Key,
                            Value = v.Value.Value,
                            IsSecret = v.Value.IsSecret,
                            IsReadOnly = v.Value.IsReadOnly
                        }).ToList()
                    };

                    return vargroup;


                }).ToList();

            } catch (Exception ex) {
            }

            return variableGroups;
        }

        #endregion Organization Operations


        #region Git File Operations
        public async Task<DataObjects.GitUpdateResult> CreateGitFile(string projectId, string repoId, string branch, string filePath, string fileContent, string pat, string orgName)
        {
            var result = new DataObjects.GitUpdateResult();
            using (var connection = CreateConnection(pat, orgName)) {
                var gitClient = connection.GetClient<GitHttpClient>();

                // Check if the file already exists using explicit parameters
                bool fileExists = false;
                try {
                    var existingItem = await gitClient.GetItemAsync(
                        project: projectId,
                        repositoryId: repoId,
                        path: filePath,
                        scopePath: null,
                        recursionLevel: VersionControlRecursionType.None,
                        includeContent: false,
                        versionDescriptor: null);
                    fileExists = existingItem != null;
                } catch {
                    fileExists = false;
                }

                if (fileExists) {
                    throw new Exception("File already exists.");
                }

                // Lookup the latest commit ID for the branch
                try {
                    var branchRefs = await gitClient.GetRefsAsync(new Guid(projectId), new Guid(repoId), includeMyBranches: true);
                    var branchRef = branchRefs.FirstOrDefault();
                    if (branchRef == null) {
                        throw new Exception($"Branch '{branch}' not found.");
                    }
                    var latestCommitId = branchRef.ObjectId;

                    // Build the changes for creating the file
                    var changes = new List<GitChange>
                    {
                        new GitChange
                        {
                            ChangeType = VersionControlChangeType.Add,
                            Item = new GitItem { Path = filePath },
                            NewContent = new ItemContent
                            {
                                Content = fileContent,
                                ContentType = ItemContentType.RawText
                            }
                        }
                    };

                    // Create the push object with proper branch reference update
                    var push = new GitPush {
                        Commits = new List<GitCommitRef>
                        {
                            new GitCommitRef
                            {
                                Comment = "Creating file",
                                Changes = changes
                            }
                        },
                        RefUpdates = new List<GitRefUpdate>
                        {
                            new GitRefUpdate
                            {
                                Name = $"refs/heads/{branch}",
                                OldObjectId = latestCommitId
                            }
                        }
                    };

                    try {
                        GitPush updatedPush = await gitClient.CreatePushAsync(push, projectId, repoId);
                        result.Success = updatedPush != null;
                        result.Message = updatedPush != null ? "File created successfully." : "File creation failed.";
                    } catch (Exception ex) {
                        result.Success = false;
                        result.Message = $"Error creating file: {ex.Message}";
                    }
                } catch (Exception ex) {
                    result.Success = false;
                    result.Message = $"Error creating file: {ex.Message}";
                }
            }
            return result;
        }

        public async Task<DataObjects.GitUpdateResult> DeleteGitFile(string projectId, string repoId, string branch, string filePath, string pat, string orgName)
        {
            var result = new DataObjects.GitUpdateResult();
            using (var connection = CreateConnection(pat, orgName)) {
                var gitClient = connection.GetClient<GitHttpClient>();
                GitItem existingItem;
                try {
                    existingItem = await gitClient.GetItemAsync(
                        project: projectId,
                        repositoryId: repoId,
                        path: filePath,
                        scopePath: null,
                        recursionLevel: VersionControlRecursionType.None,
                        includeContent: false,
                        versionDescriptor: null);
                } catch (Exception ex) {
                    throw new Exception($"File not found: {ex.Message}");
                }
                if (existingItem == null) {
                    throw new Exception("File does not exist.");
                }

                var changes = new List<GitChange>
                {
                    new GitChange
                    {
                        ChangeType = VersionControlChangeType.Delete,
                        Item = new GitItem { Path = filePath }
                    }
                };
                var commit = new GitCommitRef {
                    Comment = "Deleting file",
                    Changes = changes
                };
                var push = new GitPush {
                    Commits = new List<GitCommitRef> { commit },
                    RefUpdates = new List<GitRefUpdate>
                    {
                        new GitRefUpdate
                        {
                            Name = $"refs/heads/{branch}",
                            OldObjectId = existingItem.CommitId
                        }
                    }
                };
                try {
                    GitPush updatedPush = await gitClient.CreatePushAsync(push, projectId, repoId);
                    result.Success = updatedPush != null;
                    result.Message = updatedPush != null ? "File deleted successfully." : "File deletion failed.";
                } catch (Exception ex) {
                    result.Success = false;
                    result.Message = $"Error deleting file: {ex.Message}";
                }
            }
            return result;
        }

        public async Task<DataObjects.GitUpdateResult> EditGitFile(string projectId, string repoId, string branch, string filePath, string fileContent, string pat, string orgName)
        {
            var result = new DataObjects.GitUpdateResult();
            using (var connection = CreateConnection(pat, orgName)) {
                var gitClient = connection.GetClient<GitHttpClient>();
                GitItem existingItem;
                try {
                    existingItem = await gitClient.GetItemAsync(
                        project: projectId,
                        repositoryId: repoId,
                        path: filePath,
                        scopePath: null,
                        recursionLevel: VersionControlRecursionType.None,
                        includeContent: false,
                        versionDescriptor: null);
                } catch (Exception ex) {
                    throw new Exception($"File not found: {ex.Message}");
                }
                if (existingItem == null) {
                    throw new Exception("File does not exist.");
                }

                var changes = new List<GitChange>
                {
                    new GitChange
                    {
                        ChangeType = VersionControlChangeType.Edit,
                        Item = new GitItem { Path = filePath },
                        NewContent = new ItemContent
                        {
                            Content = fileContent,
                            ContentType = ItemContentType.RawText
                        }
                    }
                };

                var commit = new GitCommitRef {
                    Comment = "Editing file",
                    Changes = changes
                };
                var push = new GitPush {
                    Commits = new List<GitCommitRef> { commit },
                    RefUpdates = new List<GitRefUpdate>
                    {
                        new GitRefUpdate
                        {
                            Name = $"refs/heads/{branch}",
                            OldObjectId = existingItem.CommitId
                        }
                    }
                };
                try {
                    GitPush updatedPush = await gitClient.CreatePushAsync(push, projectId, repoId);
                    result.Success = updatedPush != null;
                    result.Message = updatedPush != null ? "File edited successfully." : "File edit failed.";
                } catch (Exception ex) {
                    result.Success = false;
                    result.Message = $"Error editing file: {ex.Message}";
                }
            }
            return result;
        }

        public async Task<string> GetGitFileContent(string projectId, string repoId, string branch, string filePath, string pat, string orgName)
        {
            using (var connection = CreateConnection(pat, orgName)) {
                var gitClient = connection.GetClient<GitHttpClient>();
                var versionDescriptor = new GitVersionDescriptor {
                    Version = branch,
                    VersionType = GitVersionType.Branch
                };
                try {
                    var item = await gitClient.GetItemAsync(
                        project: projectId,
                        repositoryId: repoId,
                        path: filePath,
                        scopePath: null,
                        recursionLevel: VersionControlRecursionType.None,
                        includeContent: true,
                        versionDescriptor: versionDescriptor);
                    return item.Content;
                } catch (Exception ex) {
                    throw new Exception($"Error retrieving file content: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// NEW: Retrieves the line count and character count for a Git file.
        /// Checks the in-memory cache before fetching the file content.
        /// </summary>
        public async Task<(int lineCount, int charCount, bool fromCache)> GetGitFileLineCount(string projectId, string repoId, string branch, string filePath, string pat, string orgName)
        {
            string cacheKey = $"GitFileMetadata:{projectId}:{repoId}:{branch}:{filePath}";
            if (_cache.TryGetValue(cacheKey, out (int lineCount, int charCount, bool fromCache) cachedMetadata)) {
                return (cachedMetadata.lineCount, cachedMetadata.charCount, true);
            }

            // Not cached: fetch file content, compute metadata, then cache it.
            string content = await GetGitFileContent(projectId, repoId, branch, filePath, pat, orgName);
            int lineCount = content.Split('\n').Length;
            int charCount = content.Length;
            var metadata = (lineCount, charCount, false);
            _cache.Set(cacheKey, metadata, TimeSpan.FromMinutes(5));
            return metadata;
        }

        public async Task<DataObjects.DevopsFileStructure> GetGitFileList(string projectId, string repoId, string branch, string pat, string orgName)
        {
            var output = new DataObjects.DevopsFileStructure();
            using (var connection = CreateConnection(pat, orgName)) {
                var gitClient = connection.GetClient<GitHttpClient>();
                var versionDescriptor = new GitVersionDescriptor {
                    Version = branch,
                    VersionType = GitVersionType.Branch
                };
                try {
                    var items = await gitClient.GetItemsAsync(
                        project: projectId,
                        repositoryId: repoId,
                        scopePath: null,
                        recursionLevel: VersionControlRecursionType.Full,
                        includeLinks: true,
                        versionDescriptor: versionDescriptor);
                    if (items != null && items.Any()) {
                        foreach (var item in items) {
                            if (!item.IsFolder) {
                                var fileItem = new DataObjects.DevopsFileItem {
                                    Path = item.Path,
                                    FileType = Path.GetExtension(item.Path),
                                };
                                output.Files.Add(fileItem);
                            }
                        }
                    }
                } catch (Exception ex) {
                    throw new Exception($"Error retrieving file list: {ex.Message}");
                }
            }
            return output;
        }

        public async Task<DataObjects.GitUpdateResult> UpdateGitFileContent(string projectId, string repoId, string branch, string filePath, string fileContent, string pat, string orgName)
        {
            var result = new DataObjects.GitUpdateResult();
            using (var connection = CreateConnection(pat, orgName)) {
                var gitClient = connection.GetClient<GitHttpClient>();
                var versionDescriptor = new GitVersionDescriptor {
                    Version = branch,
                    VersionType = GitVersionType.Branch
                };

                GitItem existingItem = null;
                try {
                    existingItem = await gitClient.GetItemAsync(
                        project: projectId,
                        repositoryId: repoId,
                        path: filePath,
                        scopePath: null,
                        recursionLevel: VersionControlRecursionType.None,
                        includeContent: false,
                        versionDescriptor: versionDescriptor);
                } catch {
                    existingItem = null;
                }

                var changes = new List<GitChange>();
                if (existingItem != null) {
                    changes.Add(new GitChange {
                        ChangeType = VersionControlChangeType.Edit,
                        Item = new GitItem { Path = filePath },
                        NewContent = new ItemContent {
                            Content = fileContent,
                            ContentType = ItemContentType.RawText
                        }
                    });
                } else {
                    changes.Add(new GitChange {
                        ChangeType = VersionControlChangeType.Add,
                        Item = new GitItem { Path = filePath },
                        NewContent = new ItemContent {
                            Content = fileContent,
                            ContentType = ItemContentType.RawText
                        }
                    });
                }

                var commit = new GitCommitRef {
                    Comment = existingItem != null ? "Updating file" : "Adding file",
                    Changes = changes
                };

                var push = new GitPush {
                    Commits = new List<GitCommitRef> { commit }
                };

                string oldObjectId;
                if (existingItem != null) {
                    oldObjectId = existingItem.CommitId;
                } else {
                    var branchRefs = await gitClient.GetRefsAsync(projectId, filter: $"refs/heads/{branch}");
                    var branchRef = branchRefs.FirstOrDefault();
                    if (branchRef == null) {
                        throw new Exception($"Branch '{branch}' not found.");
                    }
                    oldObjectId = branchRef.ObjectId;
                }

                push.RefUpdates = new List<GitRefUpdate>
                {
                    new GitRefUpdate
                    {
                        Name = $"refs/heads/{branch}",
                        OldObjectId = oldObjectId
                    }
                };

                try {
                    GitPush updatedPush = await gitClient.CreatePushAsync(push, projectId, repoId);
                    result.Success = updatedPush != null;
                    result.Message = updatedPush != null ? "Push completed successfully." : "Push failed.";
                } catch (Exception ex) {
                    result.Success = false;
                    result.Message = $"Error during push: {ex.Message}";
                }
            }
            return result;
        }
        #endregion Git File Operations

        #region Pipeline Operations
        public async Task<DataObjects.BuildDefinition> CreatePipeline(DataObjects.PipelineCreationRequest request, string projectId, string repoId, string branch, string pat, string orgName)
        {

            // ok first we need to create the yml, file if it does not exist, so lets do that
            DataObjects.DevopsFileStructure fileStructure = await GetGitFileList(projectId, repoId, branch, pat, orgName);

            var existingYmlFile = fileStructure?.Files?.FirstOrDefault(o => o.Path == request.YamlFilePath);

            if (existingYmlFile == null) {
                await CreateGitFile(projectId, repoId, branch, request.YamlFilePath, "{\n\n}", pat, orgName);
            }

            string ymlFilePathTrimmed = request?.YamlFilePath?.TrimStart('/', '\\') ?? string.Empty;

            using (var connection = CreateConnection(pat, orgName)) {
                try {
                    var buildClient = connection.GetClient<BuildHttpClient>();
                    var definition = new Microsoft.TeamFoundation.Build.WebApi.BuildDefinition {
                        Name = request.Name,
                        Path = "/Projects/" + request.ProjectName,
                        Project = new TeamProjectReference {
                            Id = new Guid(projectId),
                        },
                        Repository = new BuildRepository {
                            Id = repoId,
                            Type = "TfsGit",
                            DefaultBranch = request.DefaultBranch,
                        },
                        Process = new YamlProcess { YamlFilename = ymlFilePathTrimmed },
                        QueueStatus = DefinitionQueueStatus.Enabled
                    };
                    var createdDefinition = await buildClient.CreateDefinitionAsync(definition);
                    return MapBuildDefinition(createdDefinition);
                } catch (Exception ex) {
                    throw new Exception($"Error creating pipeline: {ex.Message}");
                }
            }
        }

        public async Task DeletePipeline(int pipelineId, string projectId, string pat, string orgName)
        {
            using (var connection = CreateConnection(pat, orgName)) {
                try {
                    var buildClient = connection.GetClient<BuildHttpClient>();
                    await buildClient.DeleteDefinitionAsync(projectId, pipelineId);
                } catch (Exception ex) {
                    throw new Exception($"Error deleting pipeline: {ex.Message}");
                }
            }
        }

        public async Task<DataObjects.BuildDefinition> GetPipeline(int pipelineId, string projectId, string pat, string orgName)
        {
            using (var connection = CreateConnection(pat, orgName)) {
                try {
                    var buildClient = connection.GetClient<BuildHttpClient>();
                    var definition = await buildClient.GetDefinitionAsync(projectId, pipelineId);
                    return MapBuildDefinition(definition);
                } catch (Exception ex) {
                    throw new Exception($"Error retrieving pipeline: {ex.Message}");
                }
            }
        }

        public async Task<List<DataObjects.DevOpsBuild>> GetPipelineRuns(int pipelineId, string projectId, string pat, string orgName, int skip = 0, int top = 10)
        {
            using (var connection = CreateConnection(pat, orgName)) {
                try {
                    var buildClient = connection.GetClient<BuildHttpClient>();
                    var builds = await buildClient.GetBuildsAsync(projectId, definitions: new List<int> { pipelineId });
                    var pagedBuilds = builds.Skip(skip).Take(top).ToList();
                    var devOpsBuilds = pagedBuilds.Select(b => {

                        dynamic resource = b.Links.Links["web"];
                        var url = Uri.EscapeUriString(string.Empty + resource.Href);

                        var item = new DataObjects.DevOpsBuild {
                            Id = b.Id,
                            Status = b.Status.ToString(),
                            Result = b.Result.HasValue ? b.Result.Value.ToString() : "",
                            QueueTime = b?.QueueTime ?? DateTime.UtcNow,
                            ResourceUrl = url
                        };

                        return item;
                    }).ToList();
                    return devOpsBuilds;
                } catch (Exception ex) {
                    throw new Exception($"Error getting pipeline runs: {ex.Message}");
                }
            }
        }

        public async Task<List<DataObjects.DevopsPipelineDefinition>> GetPipelines(string projectId, string pat, string orgName)
        {
            using (var connection = CreateConnection(pat, orgName)) {
                try {
                    var buildClient = connection.GetClient<BuildHttpClient>();
                    var definitions = await buildClient.GetDefinitionsAsync(project: projectId);
                    var pipelines = new List<DataObjects.DevopsPipelineDefinition>();


                    foreach (var defRef in definitions) {
                        try {
                            var fullDef = await buildClient.GetDefinitionAsync(projectId, defRef.Id);
                            dynamic pipelineReferenceLink = fullDef.Links.Links["web"];
                            var pipelineUrl = Uri.EscapeUriString(string.Empty + pipelineReferenceLink.Href);
                            string yamlFilename = string.Empty;
                            if (fullDef.Process is YamlProcess yamlProcess) {
                                yamlFilename = yamlProcess.YamlFilename;
                            }
                            pipelines.Add(new DataObjects.DevopsPipelineDefinition {
                                Id = defRef.Id,
                                Name = defRef?.Name ?? string.Empty,
                                QueueStatus = defRef?.QueueStatus.ToString() ?? string.Empty,
                                YamlFileName = yamlFilename,
                                Path = defRef.Path,
                                RepoGuid = fullDef?.Repository?.Id.ToString() ?? string.Empty,
                                RepositoryName = fullDef?.Repository?.Name ?? string.Empty,
                                DefaultBranch = fullDef?.Repository?.DefaultBranch ?? string.Empty,
                                ResourceUrl = pipelineUrl

                            });
                        } catch (Exception innerEx) {
                            Console.WriteLine($"Error retrieving full definition for pipeline {defRef.Id}: {innerEx.Message}");
                        }
                    }
                    return pipelines;
                } catch (Exception ex) {
                    throw new Exception($"Error getting pipelines: {ex.Message}");
                }
            }
        }

        public async Task<DataObjects.BuildDefinition> UpdatePipeline(DataObjects.BuildDefinition definition, string projectId, string pat, string orgName)
        {
            using (var connection = CreateConnection(pat, orgName)) {
                try {
                    var buildClient = connection.GetClient<BuildHttpClient>();
                    var fullDefinition = await buildClient.GetDefinitionAsync(projectId, definition.Id);

                    try {
                        string fileContent = await GetGitFileContent(
                            projectId,
                            definition.RepoGuid,
                            definition.DefaultBranch,
                            definition.YamlFileName,
                            pat,
                            orgName);
                    } catch (Exception ex) {
                        throw new Exception("YAML file not found: " + ex.Message);
                    }

                    fullDefinition.Name = definition.Name;
                    if (fullDefinition.Repository == null) {
                        fullDefinition.Repository = new BuildRepository();
                    }
                    fullDefinition.Repository.Id = definition.RepoGuid;
                    fullDefinition.Repository.Name = definition.RepositoryName;
                    fullDefinition.Repository.DefaultBranch = definition.DefaultBranch;
                    fullDefinition.Repository.Type = "TfsGit";

                    if (fullDefinition.Process is YamlProcess yp) {
                        yp.YamlFilename = definition.YamlFileName;
                    } else {
                        fullDefinition.Process = new YamlProcess { YamlFilename = definition.YamlFileName };
                    }
                    fullDefinition.QueueStatus = DefinitionQueueStatus.Enabled;

                    var result = await buildClient.UpdateDefinitionAsync(fullDefinition, projectId);
                    return MapBuildDefinition(result);
                } catch (Exception ex) {
                    throw new Exception($"Error updating pipeline: {ex.Message}");
                }
            }
        }

        // Private mapping helper for pipeline definitions
        private DataObjects.BuildDefinition MapBuildDefinition(Microsoft.TeamFoundation.Build.WebApi.BuildDefinition src)
        {
            dynamic resource = src.Links.Links["web"];
            var url = Uri.EscapeUriString(string.Empty + resource.Href);

            return new DataObjects.BuildDefinition {
                Id = src.Id,
                Name = src.Name ?? "",
                QueueStatus = src.QueueStatus.ToString() ?? "",
                YamlFileName = (src.Process is YamlProcess yp ? yp.YamlFilename : ""),
                RepoGuid = src.Repository?.Id.ToString() ?? "",
                RepositoryName = src.Repository?.Name ?? "",
                DefaultBranch = src.Repository?.DefaultBranch ?? "",
                ResourceUrl = url
            };
        }
        #endregion Pipeline Operations
    }
}
