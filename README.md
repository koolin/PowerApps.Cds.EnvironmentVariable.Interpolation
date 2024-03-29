# Power Apps Environment Variables String Interpolation

Implements a C# string interpolation for Power Apps Environment Variables to allow environment variables to be dynamically used in entity record data.

## Overview of Technologies

### Power Apps Environment Variables

"Environment variables as configurable input parameters allow management of data separately compared to hard-coding values within your customization or using additional tools."

<https://docs.microsoft.com/en-us/powerapps/maker/common-data-service/environmentvariables>

### C# - String Interpolation

"String interpolation provides a more readable and convenient syntax to create formatted strings than a string composite formatting feature."

<https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated>

## Implementation Details

To allow dynamic content containing values of environment variables a CDS retrieve plugin is attached to entities as a PostOperation.

The plugin step is registered with an unsecure configuration to define the input and output attribute schema names.

### Unsecure Configuration

```xml
<Settings>
  <setting name="input">
    <value>msdyn_name</value>
  </setting>
  <setting name="output">
    <value>msdyn_uri</value>
  </setting>
  <setting name="prefix">
    <value>env</value>
  </setting>
</Settings>
```

**Input (required):** the attribute with the string interpolation format string

**Output (optional):** the attribute to output the interpolation value.  If not set then the input attribute will also be the output attribute.

**Prefix (optional):** the leading text inside the format string.  For example `{env:demo_name}` the prefix value would be `env`.  If not set then no prefix is defined and standard interpolation format string is used `{demo_name}`.

## Use instructions

### Register Plugin Assembly: EntityRetrieve

Either:
* Install solution (managed OR unmanaged) - currently not available in repo

* Compile and register plugin assembly using SDK or other tools.

Register new step under assembly plugin Cds.EnvironmentVariable.Interpolation.Plugins.EntityRetrieve for desired entity using the following settings:

* **Message:** Retrieve
* **Primary Entity:** `<select your desired entity>`
* **Event Pipeline Stage of Execution:** PostOperation
* **Execution Mode:** Synchronous

Set the Unsecure configuration with XML definition.  Must at least include input setting and value.

Set your string interpolation format string in the entity records input field.

**Note:** *If no output is set then input attribute will be set and on redisplay (after a save or close and open) will no longer be visible but is still the interpolation format string that was input in the database.  The interpolation format string can be overwritten if the record modification in the UI of any fields occurs.*

### Setup your string interpolation format string

When inputting your format string in the input attribute you need to start with $.  String interpolation uses the dollar symbol as a special character to indentify a string as an interpolated string.

Example interpolated string for environment variables: "$hello {env:demo_test}"
