# Seq Azure DevOps (TFS / VSTS) [![Build status](https://ci.appveyor.com/api/projects/status/u41gy0ai2ekr5t49?svg=true)](https://ci.appveyor.com/project/xantari/seq-app-azure-devops) [![NuGet tag](https://img.shields.io/badge/nuget-seq--app-blue.svg)](https://www.nuget.org/packages?q=Seq.App.Azure.DevOps)

Azure DevOps App for the [Seq](http://getseq.net) event server.

**Important note:** This Seq App packages require Seq 5.0 or later.

This Seq App allows you to create Tasks and Bugs from within your Seq logging server and send them to Azure DevOps (aka Team Foundation Server (TFS), aka Visual Studio Team Services (VSTS))

For help with Field Definitions to place in the various configuration settings see this site:  
https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/fields/list?view=azure-devops-rest-5.0

## Fields

**Azure DevOps Url:** This URL would be the pointer to your Azure DevOps site. The trailing backslash should be included.   
Example: https://yoursite.visualstudio.com/ or https://dev.azure.com/{your organization}/  

**Project:** This is the {your project} part of your project URL (Example: https://yoursite.visualstudio.com/{your project} or https://dev.azure.com/{your organization}/{your project}).

**Azure DevOps Personal Access Token:** This would be your Personal Access Token retrieved from Azure DevOps so that the Seq Server can issue Web API requests to DevOps REST API.

**Comma seperated list of event levels (optional):** This field allows you to enter the Seq event level filters. This is only useful if you decide to stream incoming events to Azure DevOps. It allows you to set `Informational`, `Warning`, `Error`, `Fatal`, `Debug` values. The list should be comma seperated.

**Title:** The Title Field defaults to "Seq Event - {message}", where {message} is the default Seq message you see on the Seq log page. You can customize the title with any of the Seq property names. The following built in tokens are provided: SeqEventId, SeqLevel, SeqTimestamp, SeqEventUrl, SeqPropertiesList, SeqException

>NOTE: For this to work properly you would need to ensure you use property names that would exist on all types of log events.

**Description:** The Description Field defaults to the following format:

```html
<strong>Event Id:</strong> {SeqEventId}<br/>
<strong>Level:</strong> {SeqLevel}<br/>
<strong>Timestamp:</strong> {SeqTimestamp}<br/>
<strong>Event Url:</strong> <a href="{SeqEventUrl}" target="_blank">Seq Event Url</a><br/>
{SeqPropertiesList>
<strong>Message:</strong>: {message} <br/>
{SeqException}
```

The following built in tokens are provided: SeqEventId, SeqLevel, SeqTimestamp, SeqEventUrl, SeqPropertiesList, SeqException.

>NOTE: For this to work properly you would need to ensure you use property names that would exist on all types of log events.

**Description Mapping Field:** The field that the Description field listed above would map to in Azure DevOps. This is provided as different types of work items in DevOps would require the description to go into different fields. For Bugs using CMMI this typically be Microsoft.VSTS.CMMI.Symptom, For CMMI Tasks it would be: System.Description. For Bugs in Scrumm you might use Repro Steps: Microsoft.VSTS.TCM.ReproSteps

>NOTE: See the field definitions link above for help on field names.

**Tags:** The tags is a comma seperated list of tags to add to the work item.

**Area Path:** The area path is the area path defined in Azure DevOps to add it to. If not defined it will take the default area path.

**Iteration:** The iteration to assign the work item to. If not defined it will take the default iteration.

**Seq Event Id custom field # within DevOps:** This field is a custom field you would add to your work items in Azure DevOps to track the event id from Seq. This would allow the system to prevent duplicate submissions to Azure DevOps.

**Issue Type:** The issue type to create. Valid values are: Task, Bug

**Seq to DevOps property mapping:** Maps Seq properties to DevOps properties.  
Format: SeqProperty:DevOpsProperty.  
Multiple values seperated by Commas.  
`Example:` Application:Microsoft.VSTS.Build.FoundIn,MachineName:Microsoft.VSTS.TCM.SystemInfo 

>NOTE: See the field definitions link above for help on field names.

**DevOps property mappings:** Maps DevOps properties to staticly defined values.  
Format: DevOpsProperty:StaticValue  
Multiple values seperated by Commas.  
`Example:` Priority:2,Triage:Level 1

>NOTE: See the field definitions link above for help on field names.

## FAQ

`Question:` I am getting the following error when trying to send my work item:
System.AggregateException: One or more errors occurred. ---> Microsoft.VisualStudio.Services.Common.VssServiceException: TF401326: Invalid field status 'InvalidType' for field 'Microsoft.VSTS.Scheduling.Size'.

`Answer:` If you lookup the field type indicated (Microsoft.VSTS.Scheduling.Size) you will notice it is a double according to the Microsoft documentation. Check to ensure you are mapping a numeric value to that field.

`Question:` My property is not getting mapped but the work item creation is successful.

`Answer:` Seq Property names and DevOps property names are case sensitive. Turn on debug mode and ensure you are typing things in the proper case and that it is finding all Seq properties correctly.

## Screenshots

![Seq.App.Azure.DevOps](https://github.com/xantari/Seq.App.Azure.DevOps/assets/ExampleBugSetup.png)

## Authors
* Matt Olson [@xantari](https://github.com/xantari)
* Christopher Baker [@delubear](https://github.com/Delubear)

## Credits
Some code copied from Ali Özgür [@aliozgur](https://twitter.com/aliozgur)  
Thanks to jsDeliver for their CDN services!