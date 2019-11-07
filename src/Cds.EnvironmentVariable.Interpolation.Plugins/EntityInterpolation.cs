using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cds.EnvironmentVariable.Interpolation.Plugins
{
    public class EntityInterpolation
    {
        private readonly ITracingService _tracingService;
        private readonly OrganizationServiceContext _context;

        public EntityInterpolation(ITracingService tracingService, IOrganizationService service)
        {
            _tracingService = tracingService;
            _context = new OrganizationServiceContext(service);
        }
        public Entity Interpolate(Entity entity, string inputAttribute, string outputAttribute, string prefix)
        {
            if (!entity.Attributes.Contains(inputAttribute))
            {
                _tracingService.Trace($"Does not contain input attribute exiting.");
                return entity;
            }

            var input = (string)entity.Attributes[inputAttribute];
            _tracingService.Trace($"Retrieve input attribute value: {input}");

            if (!input.StartsWith("$"))
            {
                _tracingService.Trace($"DOES NOT START WITH interopolation $ exiting.");
                return entity;
            }

            input = input.Substring(1, input.Length - 1);

            if (entity.Attributes.Contains(outputAttribute))
            {
                var output = (string)entity.Attributes[outputAttribute];
                _tracingService.Trace($"Retrieve output attribute value (pre transform): {output}");
            }
            else
            {
                _tracingService.Trace($"Retrieve output attribute not included in business entity, might be null, continuing...");
            }

            var prefixString = string.IsNullOrEmpty(prefix) ? string.Empty : $"{prefix}:";
            var regexString = @"(\{" + prefixString + @"\w+})";
            _tracingService.Trace($"regex used against input: {regexString}");

            var rx = new Regex(regexString, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var matches = rx.Matches(input);
            _tracingService.Trace($"environment variable replace matches: {matches.Count}");

            foreach (var item in matches)
            {
                var schemaName = item.ToString().Replace("{" + prefixString, "").Replace("}", "");
                _tracingService.Trace($"environment variable: {schemaName}");

                var environmentVariableDef = _context.CreateQuery("environmentvariabledefinition").FirstOrDefault(a => a.GetAttributeValue<string>("schemaname").Equals(schemaName));

                if (environmentVariableDef == null)
                {
                    _tracingService.Trace($"unable to find definition for schema name: {schemaName}");
                    continue;
                }

                var defaultValue = environmentVariableDef.GetAttributeValue<string>("defaultvalue");

                _tracingService.Trace($"environment variable {schemaName}, default value: {defaultValue}");

                var environmentVariableValue = _context.CreateQuery("environmentvariablevalue").FirstOrDefault(a => a.GetAttributeValue<EntityReference>("environmentvariabledefinitionid").Id == environmentVariableDef.Id);

                var value = defaultValue ?? item.ToString();
                if (environmentVariableValue != null)
                {
                    value = environmentVariableValue.GetAttributeValue<string>("value");
                    _tracingService.Trace($"environment variable {schemaName}, value found: {value}");
                }

                input = input.Replace(item.ToString(), value);

                _tracingService.Trace($"input value updated based on environment variable {schemaName}, {input}");
            }

            _tracingService.Trace($"finalize input value: {input}");
            entity.Attributes[outputAttribute] = input;

            _tracingService.Trace($"output attribute set.  completed.");

            return entity;
        }
    }
}
