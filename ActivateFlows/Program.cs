using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace PowerPlatform.Dataverse.CodeSamples
{
    internal class Program
    {
        /// <summary>
        /// Contains the application's configuration settings. 
        /// </summary>
        private IConfiguration Configuration { get; }

        private static readonly string path = "appsettings.json";

        static void Main(string[] args)
        {
            Program app = new();

            // Grab the settings from the appsettings.json file
            var connectionString = app.Configuration.GetConnectionString("default");

            ServiceClient serviceClient = app.CreateServiceClient(connectionString);
            app.PromptUserForInitialAction(serviceClient);
        }

        /// <summary>
        /// Constructor. Loads the application configuration settings from a JSON file.
        /// </summary>
        Program()
        {
            // Load the app's configuration settings from the JSON file.
            Configuration = new ConfigurationBuilder()
                .AddJsonFile(path, optional: false, reloadOnChange: true)
                .Build();
        }

        private ServiceClient CreateServiceClient(string? connectionString)
        {
            if (connectionString is null)
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            // Create a Dataverse service client using the default connection string.
            Console.Write("Connecting to Dataverse environment...");
            ServiceClient serviceClient = new(connectionString);

            if (!serviceClient.IsReady)
            {
                throw serviceClient.LastException;
            }
            Console.WriteLine("done.");

            return serviceClient;
        }

        private Guid getSYSTEM(ServiceClient serviceClient)
        {
            var query = new QueryExpression("systemuser")
            {
                ColumnSet = new ColumnSet(false),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions =
                    {
                        new ConditionExpression("fullname",
                        ConditionOperator.Equal,
                        "SYSTEM")
                    }
                }
            };
            return serviceClient.RetrieveMultiple(query)?.Entities?.FirstOrDefault()?.Id ?? new Guid();
        }

        private void ListClassicFlows(ServiceClient serviceClient)
        {
            var query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet(
                                    "createdby",
                                    "createdon",
                                    "description",
                                    "modifiedby",
                                    "modifiedon",
                                    "name",
                                    "ownerid",
                                    "workflowid",
                                    "workflowidunique"),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions = {
                        {  new ConditionExpression(
                           "category",
                                 ConditionOperator.Equal,
                                 0 // Classic Workflow
                        )},
                        {  new ConditionExpression(
                                 "statecode",
                                 ConditionOperator.Equal,
                                 0 // Off
                        )}
                    }
                }
            };

            EntityCollection workflows = serviceClient.RetrieveMultiple(query);

            Console.WriteLine($"Total Inactive Workflows Found: {workflows.Entities.Count}");

            if (workflows.Entities.Count > 0)
            {
                printFlows(workflows.Entities.ToList());
                PromptUserForAction(workflows.Entities.ToList(), serviceClient);
            }
        }

        private void ListBusinessRules(ServiceClient serviceClient)
        {
            var query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet(
                                    "createdby",
                                    "createdon",
                                    "description",
                                    "modifiedby",
                                    "modifiedon",
                                    "name",
                                    "ownerid",
                                    "workflowid",
                                    "workflowidunique"),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions = {
                        {  new ConditionExpression(
                           "statecode",
                                 ConditionOperator.Equal,
                                 0 // Off
                        )},
                        { new ConditionExpression(
                            "category",
                            ConditionOperator.Equal,
                            2 // Business Rule
                        )}
                    }
                }
            };

            EntityCollection workflows = serviceClient.RetrieveMultiple(query);
            Console.WriteLine($"Total Inactive Business Rules Found: {workflows.Entities.Count}");
            if (workflows.Entities.Count > 0)
            {
                printFlows(workflows.Entities.ToList());
                PromptUserForAction(workflows.Entities.ToList(), serviceClient);
            }
        }

        private void ListFlows(ServiceClient serviceClient)
        {
            var System = getSYSTEM(serviceClient);
            var query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet(
                                    "createdby",
                                    "createdon",
                                    "description",
                                    "modifiedby",
                                    "modifiedon",
                                    "name",
                                    "ownerid",
                                    "workflowid",
                                    "workflowidunique"),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions = {
                        { new ConditionExpression(
                           "category",
                                 ConditionOperator.Equal,
                                 5 // Modern Flow
                        )},
                        { new ConditionExpression(
                                 "statecode",
                                 ConditionOperator.Equal,
                                 0 // Off
                        )},
                        { new ConditionExpression(
                            "createdby",
                            ConditionOperator.NotEqual,
                            System
                        )}
                    }
                }
            };

            EntityCollection workflows = serviceClient.RetrieveMultiple(query);

            Console.WriteLine($"Total Inactive Workflows Found: {workflows.Entities.Count}");

            if (workflows.Entities.Count > 0)
            {
                printFlows(workflows.Entities.ToList());
                PromptUserForAction(workflows.Entities.ToList(), serviceClient);
            }
        }

        private void printFlows(List<Entity> workflows)
        {
            workflows.ForEach(flow =>
            {
                Console.WriteLine("----------------------");
                Console.WriteLine($"name: {flow["name"]}");
                Console.WriteLine($"createdby: {flow.FormattedValues["createdby"]}");
                Console.WriteLine($"createdon: {flow.FormattedValues["createdon"]}");
                Console.WriteLine($"description: {flow.GetAttributeValue<string>("description")}");
                Console.WriteLine($"modifiedby: {flow.FormattedValues["modifiedby"]}");
                Console.WriteLine($"modifiedon: {flow.FormattedValues["modifiedon"]}");
                Console.WriteLine($"ownerid: {flow.FormattedValues["ownerid"]}");
                Console.WriteLine($"workflowid: {flow["workflowid"]}");
                Console.WriteLine($"workflowidunique: {flow["workflowidunique"]}");
            });
        }

        private void ActivateFlows(List<Entity> workflows, ServiceClient serviceClient)
        {
            int successCount = 0;
            List<Entity> failedFlows = new List<Entity>();

            workflows.ForEach(flow =>
            {
                try
                {
                    Console.WriteLine($"Activating flow: {flow["name"]}");
                    flow["statecode"] = 1; // Active
                    flow["statuscode"] = 2; // Activated
                    serviceClient.Update(flow);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to activate flow: {flow["name"]}. Error: {ex.Message}");
                    failedFlows.Add(flow);
                }
            });

            if (failedFlows.Count > 0)
            {
                Console.WriteLine($"Total flows failed to activate: {failedFlows.Count}");
                PromptUserForAction(failedFlows, serviceClient);
            }
            else
            {
                Console.WriteLine("All flows activated successfully.");
                PromptUserForAction(failedFlows, serviceClient);
            }
        }

        private void PromptUserForInitialAction(ServiceClient serviceClient)
        {
            Console.WriteLine("----------------------");
            Console.WriteLine("Do you want to list the inactive (f)lows, list the inactive (c)lassic workflows, list the inactive (b)usiness rules, or (e)xit?");
            string? userInput = Console.ReadLine();

            if (userInput?.ToLower() == "f")
            {
                ListFlows(serviceClient);
            }
            else if (userInput?.ToLower() == "c")
            {
                ListClassicFlows(serviceClient);
            }
            else if (userInput?.ToLower() == "b")
            {
                ListBusinessRules(serviceClient);
            }
            else if (userInput?.ToLower() == "e")
            {
                Console.WriteLine("Exiting...");
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter 'f' to list flows, 'c' to list classic workflows, 'b' to list business rules, or 'e' to exit.");
                PromptUserForInitialAction(serviceClient);
            }
        }

        private void PromptUserForAction(List<Entity> failedFlows, ServiceClient serviceClient)
        {
            Console.WriteLine("----------------------");
            Console.WriteLine("Do you want to (a)ctivate these, list the inactive (f)lows, list the inactive (c)lassic workflows, list the inactive (b)usiness rules, or (e)xit?");
            string? userInput = Console.ReadLine();

            if (userInput?.ToLower() == "f")
            {
                printFlows(failedFlows);
                PromptUserForAction(failedFlows, serviceClient);
            }
            else if (userInput?.ToLower() == "c")
            {
                ListClassicFlows(serviceClient);
            }
            else if (userInput?.ToLower() == "b")
            {
                ListBusinessRules(serviceClient);
            }
            else if (userInput?.ToLower() == "a")
            {
                ActivateFlows(failedFlows, serviceClient);
            }
            else if (userInput?.ToLower() == "e")
            {
                Console.WriteLine("Exiting...");
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter 'l' to list, 'a' attempt activation, or 'e' to exit.");
                PromptUserForAction(failedFlows, serviceClient);
            }
        }
    }
}