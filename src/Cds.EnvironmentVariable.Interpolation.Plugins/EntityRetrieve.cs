using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Cds.EnvironmentVariable.Interpolation.Plugins
{
    public class EntityRetrieve : IPlugin
    {

        private XmlDocument _pluginConfiguration;
        public EntityRetrieve(string unsecureConfig, string secureConfig)
        {
            if (string.IsNullOrEmpty(unsecureConfig))
            {
                throw new InvalidPluginExecutionException("Unsecure configuration missing.");
            }
            _pluginConfiguration = new XmlDocument();
            _pluginConfiguration.LoadXml(unsecureConfig);
        }

        public static string GetConfigDataString(XmlDocument doc, string label)
        {
            return GetValueNode(doc, label);
        }
        private static string GetValueNode(XmlDocument doc, string key)
        {
            XmlNode node = doc.SelectSingleNode(String.Format("Settings/setting[@name='{0}']", key));
            if (node != null)
            {
                return node.SelectSingleNode("value").InnerText;
            }
            return string.Empty;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            // Extract the tracing service for use in debugging sandboxed plug-ins.  
            // If you are not registering the plug-in in the sandbox, then you do  
            // not have to add any tracing service related code.  
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The Output collection contains all the data passed in the message request.  
            if (!context.OutputParameters.Contains("BusinessEntity") && !(context.OutputParameters["BusinessEntity"] is Entity))
            {
                return;
            }

            tracingService.Trace($"unsecure configuration: {_pluginConfiguration.OuterXml}");

            var inputAttribute = GetConfigDataString(_pluginConfiguration, "input");
            var outputAttribute = GetConfigDataString(_pluginConfiguration, "output");
            var prefix = GetConfigDataString(_pluginConfiguration, "prefix");



            if (string.IsNullOrEmpty(inputAttribute))
            {
                tracingService.Trace($"NO input attribute provided, exiting");
                return;
            }
            else
            {
                tracingService.Trace($"input attribute: {inputAttribute}");
            }

            if (string.IsNullOrEmpty(outputAttribute))
            {
                outputAttribute = inputAttribute;
                tracingService.Trace($"output attribute set to input");
            }
            else
            {
                tracingService.Trace($"output attribute: {outputAttribute}");
            }

            tracingService.Trace($"prefix: {prefix}");

            // Obtain the target entity from the output parameters.  
            // Business Entity provides for modification in post-operation
            Entity entity = (Entity)context.OutputParameters["BusinessEntity"];

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            var orgContext = new OrganizationServiceContext(service);

            var input = (string)entity.Attributes[inputAttribute];
            tracingService.Trace($"Retrieve input attribute value: {input}");

            if (!input.StartsWith("$"))
            {
                tracingService.Trace($"DOES NOT START WITH interopolation $ exiting.");
                return;
            }

            input = input.Substring(1, input.Length - 1);

            if (entity.Attributes.Contains(outputAttribute))
            {
                var output = (string)entity.Attributes[outputAttribute];
                tracingService.Trace($"Retrieve output attribute value (pre transform): {output}");
            }
            else
            {
                tracingService.Trace($"Retrieve output attribute not included in business entity, might be null, continuing...");
            }


            var prefixString = string.IsNullOrEmpty(prefix) ? string.Empty : $"{prefix}:";
            var regexString = @"(\{" + prefixString + @"\w+})";
            tracingService.Trace($"regex used against input: {regexString}");

            var rx = new Regex(regexString, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var matches = rx.Matches(input);
            tracingService.Trace($"environment variable replace matches: {matches.Count}");


            foreach (var item in matches)
            {
                var schemaName = item.ToString().Replace("{" + prefixString, "").Replace("}", "");
                tracingService.Trace($"environment variable: {schemaName}");

                var environmentVariableDef = orgContext.CreateQuery("environmentvariabledefinition").FirstOrDefault(a => a.GetAttributeValue<string>("schemaname").Equals(schemaName));

                if (environmentVariableDef == null)
                {
                    tracingService.Trace($"unable to find definition for schema name: {schemaName}");
                    continue;
                }

                var defaultValue = environmentVariableDef.GetAttributeValue<string>("defaultvalue");

                tracingService.Trace($"environment variable {schemaName}, default value: {defaultValue}");

                var environmentVariableValue = orgContext.CreateQuery("environmentvariablevalue").FirstOrDefault(a => a.GetAttributeValue<EntityReference>("environmentvariabledefinitionid").Id == environmentVariableDef.Id);

                var value = defaultValue ?? item.ToString();
                if (environmentVariableValue != null)
                {
                    value = environmentVariableValue.GetAttributeValue<string>("value");
                    tracingService.Trace($"environment variable {schemaName}, value found: {value}");
                }

                input = input.Replace(item.ToString(), value);

                tracingService.Trace($"input value updated based on environment variable {schemaName}, {input}");
            }

            tracingService.Trace($"finalize input value: {input}");
            entity.Attributes[outputAttribute] = input;

            tracingService.Trace($"output attribute set.  completed.");
        }
    }
}
