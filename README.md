# CSV to ADH sample

| :loudspeaker: **Notice**: Samples have been updated to reflect that they work on AVEVA Data Hub. The samples also work on OSIsoft Cloud Services unless otherwise noted. |
| -----------------------------------------------------------------------------------------------|  

**Version:** 1.3.1

[![Build Status](https://dev.azure.com/osieng/engineering/_apis/build/status/product-readiness/ADH/aveva.sample-adh-csv_to_adh-dotnet?branchName=main)](https://dev.azure.com/osieng/engineering/_build/latest?definitionId=2615&branchName=main)

Developed against DotNet 5.0.

## About this sample

| :loudspeaker: **Notice**: The [PI Adapter for Structured Data Files](https://osisoft.github.io/PI-Adapter-Structured-Data-Files-Docs/content/index.html) can send CSVs (and other files types) to EDS, ADH, and PI. The released adapter is recommended for use if you need to send .csv data, but this sample is available if you need to build your own solution.|

This sample sends data from a passed in csv file or from the datafile.csv file local to the application to ADH.
This sample uses the Authentication flow to authenticate against ADH. .  
By default it will create the type and the streams used in the defauly datafile.csv.
When testing it will check the values to make sure they are saved on ADH.
When testing, at the end, it will delete whatever it added to the system.

## Running this sample

In this example we assume that you have the dotnet core CLI.

### Prerequisites

- Register an Authorization Code client in ADH and ensure that the registered client in ADH contains `https://127.0.0.1:54567/signin-oidc` in the list of RedirectUris. For details on this please see this [video](https://www.youtube.com/watch?v=97QJjUKa6Pk)
- Configure the sample using the file [appsettings.placeholder.json](CSVtoADH/appsettings.placeholder.json). Before editing, rename this file to `appsettings.json`. This repository's `.gitignore` rules should prevent the file from ever being checked in to any fork or branch, to ensure credentials are not compromised.
- Replace the placeholders in the `appsettings.json` file with your Tenant Id, Client Id, and Client Secret obtained from registration.

### Configure constants for connecting and authentication

Please update the `appsettings.json` file with the appropriate information as shown below. This sample leverages PKCE login, so that way the user running this application has appropriate authorization.

```json
{
  "NamespaceId": "PLACEHOLDER_REPLACE_WITH_NAMESPACE_ID",
  "TenantId": "PLACEHOLDER_REPLACE_WITH_TENANT_ID",
  "Resource": "https://uswe.datahub.connect.aveva.com",
  "ApiVersion": "v1",
  "ClientId": "PLACEHOLDER_REPLACE_WITH_CLIENT_ID",
  "Password": "PLACEHOLDER_REPLACE_WITH_PASSWORD",
  "Username": "PLACEHOLDER_REPLACE_WITH_USERNAME"
}
```

### Using Command Line

To run this example from the commandline run:

```shell
dotnet restore
dotnet run
```

To test this program change directories to the test and run:

```shell
dotnet restore
dotnet test
```

---

For the main ADH samples page [ReadMe](https://github.com/osisoft/OSI-Samples-OCS)  
For the main AVEVA samples page [ReadMe](https://github.com/osisoft/OSI-Samples)
