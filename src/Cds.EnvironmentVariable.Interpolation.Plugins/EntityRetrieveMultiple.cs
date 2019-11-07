using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Cds.EnvironmentVariable.Interpolation.Plugins
{
    public class EntityRetrieveMultiple : IPlugin
    {

        private XmlDocument _pluginConfiguration;
        public EntityRetrieveMultiple(string unsecureConfig, string secureConfig)
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
            if (!context.OutputParameters.Contains("BusinessEntityCollection") && !(context.OutputParameters["BusinessEntityCollection"] is EntityCollection))
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
            EntityCollection entityCollection = (EntityCollection)context.OutputParameters["BusinessEntityCollection"];

            tracingService.Trace($"# of Records: {entityCollection.Entities.Count}");

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            var interpolation = new EntityInterpolation(tracingService, service);

            for (int i = 0; i < entityCollection.Entities.Count - 1; i++)
            {
                entityCollection.Entities[i] = interpolation.Interpolate(entityCollection.Entities[i], inputAttribute, outputAttribute, prefix);
            }

            
        }
    }
}
