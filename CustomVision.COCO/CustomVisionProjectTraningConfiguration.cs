using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Cognitive.CustomVision.Helpers
{
    public class ProjectTraningConfiguration
    {
        public string trainingKey
        {
            get;
            private set; 
        }

        private string trainingEndpoint;

        public CustomVisionTrainingClient trainingApi
        {
            get;
            private set;
        }

        public Project project
        {
            get;
            private set; 
        }

        public Domain projectDomain
        {
            get;
            private set;
        }

        public IList<Tag> tagList
        {
            get;
            private set;
        }


        public static async Task<ProjectTraningConfiguration> CreateProjectAsync(string trainingEndpoint, string trainingKey, string projectName, bool openIfExists, bool isDetectionModel)
        {
            ProjectTraningConfiguration result = new ProjectTraningConfiguration(trainingEndpoint, trainingKey);

            await result.CreateProjectAsyncInternal(projectName, openIfExists, isDetectionModel);

            return result;
        }

        public static async Task<ProjectTraningConfiguration> OpenProjectAsync(string trainingEndpoint, string trainingKey, Guid projectId)
        {
            ProjectTraningConfiguration result = new ProjectTraningConfiguration(trainingEndpoint, trainingKey);

            await result.OpenProjectAsyncInternal(projectId);

            return result;
        }


        public async Task<Tag> AddTagAsync(string name)
        {
            var tags = tagList.Where(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if(tags != null && tags.Count() > 0)
            {
                return tags.First();
            }

            var result = await trainingApi.CreateTagAsync(project.Id, name);
            await InitProjectTags();

            return tagList.Where(t => t.Id == result.Id).First();
        }


        private ProjectTraningConfiguration(string trainingEndpoint, string trainingKey)
        {
            this.trainingKey = trainingKey;
            this.trainingEndpoint = trainingEndpoint;
            trainingApi = new CustomVisionTrainingClient(new ApiKeyServiceClientCredentials(this.trainingKey));

            if (!string.IsNullOrEmpty(trainingEndpoint))
                trainingApi.Endpoint = trainingEndpoint;
        }

        private async Task CreateProjectAsyncInternal(string projectName, bool openIfExists, bool isDetectionModel)
        {
            await GetDefaultDomain(isDetectionModel);

            var projects = (await trainingApi.GetProjectsAsync()).Where(p => p.Name.Equals(projectName)).ToList();
            if (projects != null && projects.Count > 0)
            {
                if (openIfExists)
                {
                    this.project = projects.First();
                    await InitProjectTags();
                }
                else
                    throw new Exception($"{projectName} already exists");
            }
            else
            {
                this.project = await trainingApi.CreateProjectAsync(projectName, null, projectDomain.Id);
                if (this.project == null)
                {
                    throw new Exception($"Cannot create project {projectName}");
                }

                this.tagList = new List<Tag>();
            }

        }


        private async Task OpenProjectAsyncInternal(Guid projectId)
        {
            

            var projects = (await trainingApi.GetProjectsAsync()).Where(p => p.Id.Equals(projectId)).ToList();
            if (projects != null && projects.Count > 0)
            {
                this.project = projects.First();
            }
            else
            {
                throw new KeyNotFoundException("Cannot find project with provided projectId");
            }

            await GetDomain(this.project.Settings.DomainId);
        }

        private async Task<Domain> GetDefaultDomain(bool isDetectionModel)
        {
            if (projectDomain == null)
            {
                try
                {
                    var domains = await trainingApi.GetDomainsAsync();

                    if (isDetectionModel)
                    {
                        projectDomain = domains.FirstOrDefault(d => d.Type == "ObjectDetection");
                    }
                    else
                    {
                        projectDomain = domains.FirstOrDefault(d => d.Type == "Classification" && d.Name.Contains("compact"));
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Expetion {0} at {1}", ex.Message, ex.StackTrace);
                    throw;
                }
            }

            return projectDomain;
        }

        private async Task<Domain> GetDomain(Guid id)
        {
            if (projectDomain == null)
            {
                var domains = await trainingApi.GetDomainsAsync();
                projectDomain = domains.FirstOrDefault(d => d.Id == id);
            }

            return projectDomain;
        }

        private async Task InitProjectTags()
        {
            tagList = await trainingApi.GetTagsAsync(project.Id);
            if (tagList == null)
                tagList = new List<Tag>();
        }
    }
}
