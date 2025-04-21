using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Xrm.Sdk.Query;

namespace PowerPlatform.Dataverse.CodeSamples
{
    internal class Program
    {
        private static string clientId = "";
        private static string secret = "";
        private static ServiceClient serviceClient = null!;
        private static string? targetEnvironmentUrl = "";
        Environment? targetEnvironment = null;


        static void Main(string[] args)
        {
            Program app = new();

            // Grab the settings from the appsettings.json file

            //prompt the user to enter the target environment url
            Console.WriteLine("Please enter the target environment URL:");
            targetEnvironmentUrl = Console.ReadLine();
            if (string.IsNullOrEmpty(targetEnvironmentUrl))
            {
                Console.WriteLine("Target environment URL cannot be empty.");
                return;
            }

            //public ServiceClient(Uri instanceUrl, string clientId, string clientSecret, bool useUniqueInstance, ILogger logger = null)

            try
            {
                serviceClient = new ServiceClient(new Uri(targetEnvironmentUrl ?? ""), clientId, secret, true);
            }
            catch
            {
                Console.WriteLine("An error occurred while establishing the serviceClient.");
            }

            app.LoadConfigurationAndRetrieveData(targetEnvironmentUrl!);
        }

        Program()
        {
        }

        private void LoadConfigurationAndRetrieveData(string targetEnvUrl)
        {
            // Load the configuration file
            string configFilePath = Path.Combine(AppContext.BaseDirectory, "environmentconfig.json");
            string json = File.ReadAllText(configFilePath);
            if (string.IsNullOrEmpty(json))
            {
                Console.WriteLine("Configuration file is empty or not found.");
                return;
            }
            List<Environment>? config = JsonSerializer.Deserialize<List<Environment>>(json);
            if (config == null)
            {
                Console.WriteLine("Failed to load configuration.");
                return;
            }



            // Retrieve the list of solutions for building config document

            // QueryExpression query = new QueryExpression("solution");
            // query.ColumnSet.AddColumns("uniquename", "solutionid");

            // var result = serviceClient.RetrieveMultiple(query);
            // List<Solution> solutions;
            // if (result == null || result.Entities.Count == 0)
            // {
            //     Console.WriteLine("no solutions!");
            //     return;
            // }

            // solutions = result.Entities.ToList().Select(e => new Solution(e.GetAttributeValue<string>("uniquename"), e.Id.ToString())).ToList();



            // Retrieve the component types for friendly display of required object type codes
            var query = new QueryExpression("stringmap");
            query.ColumnSet.AddColumns("value", "attributevalue");
            query.Criteria.AddCondition("attributename", ConditionOperator.Equal, "componenttype");

            var result = serviceClient.RetrieveMultiple(query);
            if (result == null || result.Entities.Count == 0)
            {
                Console.WriteLine("no component types!");
                return;
            }
            var componentTypes = result.Entities.ToList().Select(e => new ComponentType(e.GetAttributeValue<string>("value"), e.GetAttributeValue<int>("attributevalue"))).ToList();



            //Gather Missing Dependencies and construct Solution objects
            var request = new OrganizationRequest("RetrieveMissingDependencies");
            targetEnvironment = config.Find(env => env.Url!.ToLower() == targetEnvUrl.ToLower());

            if (targetEnvironment == null)
            {
                Console.WriteLine("Target environment not found in configuration.");
                return;
            }

            targetEnvironment.Solutions!.ToList().ForEach(sol =>
            {
                request["SolutionUniqueName"] = sol.Name;
                var response = serviceClient.ExecuteOrganizationRequest(request);
                if (response != null)
                {
                    response.Results.Values.ToList().ForEach(dep =>
                    {
                        if (dep is EntityCollection collection)
                        {
                            foreach (var entity in collection.Entities)
                            {
                                if (entity.GetAttributeValue<Guid>("requiredcomponentbasesolutionid").ToString() == targetEnvironment.Solutions!.ToList().Find(s => s.Name == "Active")!.Id)
                                {
                                    var dependentObjectId = entity.GetAttributeValue<Guid>("dependentcomponentobjectid").ToString();
                                    var dependentObjectType = entity.GetAttributeValue<OptionSetValue>("dependentcomponenttype").Value;
                                    var requiredObjectId = entity.GetAttributeValue<Guid>("requiredcomponentobjectid").ToString();
                                    var requiredObjectType = entity.GetAttributeValue<OptionSetValue>("requiredcomponenttype").Value;
                                    var dependency = new Dependency(dependentObjectId, dependentObjectType, requiredObjectId, requiredObjectType);

                                    sol.MissingDependencies.Add(dependency);
                                }
                            }
                        }
                    });
                }
            });

            targetEnvironment.Solutions!.Where(s => s.Name != "Active").ToList().ForEach(s =>
            {
                query = new QueryExpression("solutioncomponent");
                query.ColumnSet.AddColumns("solutionid", "componenttype", "objectid");
                query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, s.Id);

                result = serviceClient.RetrieveMultiple(query);
                if (result == null || result.Entities.Count == 0)
                {
                    Console.WriteLine("no components!");
                    return;
                }

                s.Components = result.Entities.Select(re =>
                {
                    var componentType = componentTypes.Find(ct =>
                        ct.Id == re.GetAttributeValue<OptionSetValue>("componenttype").Value);

                    if (componentType == null)
                    {
                        Console.WriteLine($"Component type not found for component: {re.Id}");
                        return null; // Skip this component
                    }

                    var component = new Component(
                        re.Id.ToString(),
                        new ComponentType(componentType.Name, componentType.Id)
                    );

                    targetEnvironment.Solutions!.ToList().ForEach(sol =>
                    {
                        sol.MissingDependencies.Find(md => 
                            md.DependentObjectId == component.Id
                        )?.RequiredObjectFoundIn.Add(sol.Name);
                    });

                    return component;
                })
                .Where(c => c != null)
                .ToList();
            });

            // issue a report on the missing dependencies. The report will highlight the dependency relationships between solutions, and will alert the user to any situation where one solution has a dependency on another solution that is likewise dependent on the first solution. This is a circular dependency, and should be avoided.

            Console.WriteLine("Missing Dependencies Report:");
            Console.WriteLine("===================================");
            Console.WriteLine($"Target Environment: {targetEnvironment.Url}");
            Console.WriteLine("Solution Dependency Summary:");
            Console.WriteLine("===================================");
            foreach (var solution in targetEnvironment.Solutions!)
            {
                Console.WriteLine($"Solution: {solution.Name} ({solution.Id})");
                if (solution.MissingDependencies.Count > 0)
                {
                    // Create a comma-separated list of dependent solutions
                    var dependentSolutions = solution.MissingDependencies
                        .SelectMany(md => md.RequiredObjectFoundIn)
                        .Distinct() // Ensure no duplicates
                        .ToList();

                    if (dependentSolutions.Count > 0)
                    {
                        Console.WriteLine($" - Depends on: {string.Join(", ", dependentSolutions)}");
                        
                        // Check for circular dependencies
                        foreach (var dependentSolutionName in dependentSolutions)
                        {
                            var dependentSolution = targetEnvironment.Solutions.FirstOrDefault(s => s.Name == dependentSolutionName);
                            if (dependentSolution != null)
                            {
                                var circularDependency = dependentSolution.MissingDependencies
                                    .Any(md => md.RequiredObjectFoundIn.Contains(solution.Name));

                                if (circularDependency)
                                {
                                    Console.WriteLine("*****");
                                    Console.WriteLine($"ERROR: Circular dependency detected! {solution.Name} depends on {dependentSolutionName}, and {dependentSolutionName} depends on {solution.Name}.");
                                    Console.WriteLine("*****");

                                    LogCircularDependency(solution.Name, dependentSolutionName);
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine(" - No dependencies found.");
                    }
                }
                else
                {
                    Console.WriteLine(" - No missing dependencies.");
                }
            }
            Console.WriteLine("===================================");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        public void LogCircularDependency(string solutionName, string dependentSolutionName)
        {
            // Log the circular dependency to a file or database
            string logFilePath = Path.Combine(AppContext.BaseDirectory, "circular_dependencies.log");
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine($"Circular dependency detected: {solutionName} <-> {dependentSolutionName} at {DateTime.Now}");
                
                writer.WriteLine("===================================");

                //Log out a serialized version of the targetEnvironment object, omitting the components
                // to avoid cluttering the log file with too much data.
                targetEnvironment!.Solutions?.ToList().ForEach(s =>
                {
                    s.Components = null; // Set components to null to avoid cluttering the log
                });
                string serializedTargetEnvironment = JsonSerializer.Serialize(targetEnvironment, new JsonSerializerOptions { WriteIndented = true });
                writer.WriteLine(serializedTargetEnvironment);
                writer.WriteLine("===================================");
                writer.WriteLine("Press any key to continue...");
            }
        }
    }

    public class ComponentType
    {
        public string Name { get; set; }
        public int Id { get; set; }

        public ComponentType(string name, int id)
        {
            Name = name;
            Id = id;
        }
    }

    public class Component
    {
        public string Id { get; set; }
        public ComponentType Type { get; set; }

        public Component(string id, ComponentType type)
        {
            Id = id;
            Type = type;
        }
    }

    public class Environment
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("solutions")]
        public Solution[]? Solutions { get; set; }
    }

    public class Solution
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        public List<Dependency> MissingDependencies { get; set; } = new List<Dependency>();
        public List<Component?>? Components { get; set; }

        public Solution(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    public class Dependency
    {
        public string DependentObjectId { get; set; }
        public int DependentObjectType { get; set; }
        public string RequiredObjectId { get; set; }
        public int RequiredObjectType { get; set; }
        public List<string> RequiredObjectFoundIn { get; set; } = new List<string>();

        public Dependency(string dependentObjectId, int dependentObjectType, string requiredObjectId, int requiredObjectType)
        {
            DependentObjectId = dependentObjectId;
            DependentObjectType = dependentObjectType;
            RequiredObjectId = requiredObjectId;
            RequiredObjectType = requiredObjectType;
        }
    }
}