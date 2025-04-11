using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace PowerPlatform.Dataverse.CodeSamples
{
    internal class Program
    {
        private static string clientId = "";
        private static string secret = "";
        private static ServiceClient serviceClient = null!;

        static void Main(string[] args)
        {
            Program app = new();

            // Grab the settings from the appsettings.json file

            //prompt the user to enter the target environment url
            Console.WriteLine("Please enter the target environment URL:");
            string? targetEnvironmentUrl = Console.ReadLine();

            //public ServiceClient(Uri instanceUrl, string clientId, string clientSecret, bool useUniqueInstance, ILogger logger = null)

            try
            {
                serviceClient = new ServiceClient(new Uri("uri"), clientId, secret, true);
            }
            catch
            {
                Console.WriteLine("An error occurred while establishing the serviceClient.");
            }

            app.LoadConfigurationAndRetrieveData();
        }

        Program()
        {
        }

        private void LoadConfigurationAndRetrieveData()
        {
            // Load the configuration file
            string configFilePath = Path.Combine(AppContext.BaseDirectory, "environmentconfig.json");
            string json = File.ReadAllText(configFilePath);
            if(string.IsNullOrEmpty(json))
            {
                Console.WriteLine("Configuration file is empty or not found.");
                return;
            }
            EnvironmentConfig config = JsonSerializer.Deserialize<EnvironmentConfig>(json);
            if (config == null)
            {
                Console.WriteLine("Failed to load configuration.");
                return;
            }

            var request = new OrganizationRequest("RetrieveMissingDependencies");
            request["SolutionUniqueName"] = "sampleSolution";

            var response = serviceClient.ExecuteOrganizationRequest(request);
            
            if(response != null)
            {
                var missingDependencies = response["MissingDependencies"] as List<MissingDependency>;
                if (missingDependencies != null)
                {
                    foreach (var dependency in missingDependencies)
                    {
                        Console.WriteLine($"Missing Dependency: {dependency.RequiredComponentSchemaName}");
                    }
                }
                else
                {
                    Console.WriteLine("No missing dependencies found.");
                }
            }
            else
            {
                Console.WriteLine("Failed to retrieve missing dependencies.");
            }

        }

    }

    public class EnvironmentConfig
    {
        [JsonPropertyName("Environments")]
        public List<Environment> Environments { get; set; }
    }

    public class Environment
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("solutions")]
        public List<Solution> Solutions { get; set; }
    }

    public class Solution
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
    }
}