using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Xrm.Sdk.Query;

namespace PowerPlatform.Dataverse.CodeSamples
{
    internal class Program
    {
        private static string clientId = "2053b7db-a35a-4209-9e57-c819cc4d77f4";
        private static string secret = "Yt5V6_--jNYA-47j0xxRzHFqcDcZv5.TAG";
        private static ServiceClient serviceClient = null!;
        private static string? targetEnvironmentUrl = "";

        static void Main(string[] args)
        {
            Program app = new();

            // Grab the settings from the appsettings.json file

            //prompt the user to enter the target environment url
            Console.WriteLine("Please enter the target environment URL:");
            targetEnvironmentUrl = Console.ReadLine();

            //public ServiceClient(Uri instanceUrl, string clientId, string clientSecret, bool useUniqueInstance, ILogger logger = null)

            try
            {
                serviceClient = new ServiceClient(new Uri(targetEnvironmentUrl), clientId, secret, true);
            }
            catch
            {
                Console.WriteLine("An error occurred while establishing the serviceClient.");
            }

            app.LoadConfigurationAndRetrieveData(targetEnvironmentUrl);
        }

        Program()
        {
        }

        private void LoadConfigurationAndRetrieveData(string targetEnvUrl)
        {
            // Load the configuration file
            string configFilePath = Path.Combine(AppContext.BaseDirectory, "environmentconfig.json");
            string json = File.ReadAllText(configFilePath);
            if(string.IsNullOrEmpty(json))
            {
                Console.WriteLine("Configuration file is empty or not found.");
                return;
            }
            List<Environment> config = JsonSerializer.Deserialize<List<Environment>>(json);
            if (config == null)
            {
                Console.WriteLine("Failed to load configuration.");
                return;
            }

            QueryExpression query = new QueryExpression("Solution");
            query.ColumnSet.AddColumns("uniquename", "solutionid");

            var result = serviceClient.RetrieveMultiple(query);
            List<Solution> solutions;
            if(result == null || result.Entities.Count == 0)
            {
                Console.WriteLine("no solutions!");
                return;
            }
            
            solutions = result.Entities.ToList().Select(e => new Solution(e.GetAttributeValue<string>("uniquename"), e.Id.ToString())).ToList();

            //Gather Missing Dependencies and construct Solution objects
            var request = new OrganizationRequest("RetrieveMissingDependencies");
            Environment targetEnvironment = config.Find(env => env.Url.ToLower() == targetEnvUrl.ToLower());

            targetEnvironment.Solutions.ToList().ForEach(sol => {
                request["SolutionUniqueName"] = sol.Name;
                var response = serviceClient.ExecuteOrganizationRequest(request);
                if(response != null)
                {
                    var a = 1;
                }
            });

            
            // config.ForEach(env => {
                
            // });
        }
    }

    public class Environment
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("solutions")]
        public Solution[] Solutions { get; set; }
    }

    public class Solution
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public List<Dependency>? MissingDependencies { get; set; }

        public Solution(string name, string id)
        {
            Name = name;
            Id = id;
            MissingDependencies = null;
        }
    }

    public class Dependency
    {
        public string DependentObjectId { get; set; }
        public string DependentObjectType { get; set; }
        public string RequiredObjectId { get; set; }
        public string RequiredObjectType { get; set; }
        public List<string>? RequiredObjectFoundIn { get; set; }

        public Dependency(string dependentObjectId, string dependentObjectType, string requiredObjectId, string requiredObjectType)
        {
            DependentObjectId = dependentObjectId;
            DependentObjectType = dependentObjectType;
            RequiredObjectId = requiredObjectId;
            RequiredObjectType = requiredObjectType;
        }
    }
}