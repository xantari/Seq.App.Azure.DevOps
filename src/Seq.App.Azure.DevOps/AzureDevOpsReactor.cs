using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Seq.Apps;
using Seq.Apps.LogEvents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog.Parsing;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using LogEventLevel = Seq.Apps.LogEvents.LogEventLevel;

// ReSharper disable UnusedAutoPropertyAccessor.Global, UnusedType.Global, MemberCanBePrivate.Global, ArrangeTypeMemberModifiers

namespace Seq.App.Azure.DevOps
{
    [SeqApp("Azure DevOps",
        Description = "Posts seq event as a work item to Azure DevOps")]
    public class AzureDevOpsReactor : Reactor, ISubscribeToAsync<LogEventData>
    {
        #region Settings

        [SeqAppSetting(DisplayName = "Azure DevOps Url",
            HelpText = "URL of your Azure DevOps Site (Example: https://yoursite.visualstudio.com/ or https://dev.azure.com/{your organization}/).")]
        public string AzureDevOpsUrl { get; set; }

        [SeqAppSetting(DisplayName = "Project",
            HelpText = "Project Name, this is the {your project} part of your project URL (Example: https://yoursite.visualstudio.com/{your project} or https://dev.azure.com/{your organization}/{your project}).")]
        public string Project { get; set; }

        [SeqAppSetting(DisplayName = "Azure DevOps Personal Access Token",
            HelpText = "Azure DevOps Personal Access Token.")]
        public string PersonalAccessToken { get; set; }

        [SeqAppSetting(DisplayName = "Comma separated list of event levels",
            IsOptional = true,
            HelpText = "If specified Azure DevOps issue (work item or bug) will be created only for the specified event levels, other levels will be discarded")]
        public string LogEventLevels { get; set; }

        public List<LogEventLevel> LogEventLevelList
        {
            get
            {
                var result = new List<LogEventLevel>();
                if (string.IsNullOrEmpty(LogEventLevels))
                    return result;

                var strValues = LogEventLevels.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                if (strValues.Length == 0)
                    return result;

                strValues.Aggregate(result, (acc, strValue) =>
                {
                    if (Enum.TryParse(strValue, out LogEventLevel enumValue))
                        acc.Add(enumValue);
                    return acc;
                });

                return result;
            }
        }

        [SeqAppSetting(DisplayName = "Title",
            HelpText = "Title of created Azure DevOps items. Use {PropertyName} to insert properties into the field. Ex. {Application}, {Message}, etc. If not defined with will follow the format: Seq Event - {message}. Max Length is 255. The following special properties are available: SeqEventId, SeqLevel, SeqTimestamp, SeqEventUrl, SeqPropertiesList, SeqException",
            IsOptional = true)]
        public string Title { get; set; }

        [SeqAppSetting(DisplayName = "Description",
            InputType = SettingInputType.LongText,
            HelpText = "Description of created Azure DevOps items. Use {PropertyName} to insert properties into the field. Ex. {Application}, {Message}, etc. If not defined with will output all properties and exception data from Seq in a pretty format. The following special properties are available: SeqEventId, SeqLevel, SeqTimestamp, SeqEventUrl, SeqPropertiesList, SeqException",
            IsOptional = true)]
        public string Message { get; set; }

        //Microsoft.VSTS.CMMI.Symptom
        [SeqAppSetting(DisplayName = "Description Mapping Field",
            HelpText = "Description DevOps Mapping Field. For Bugs using CMMI this typically be Microsoft.VSTS.CMMI.Symptom, For CMMI Tasks it would be: System.Description. For Bugs in Scrum you might use Repro Steps: Microsoft.VSTS.TCM.ReproSteps")]
        public string DescriptionDevOpsMappingField { get; set; }

        [SeqAppSetting(
            DisplayName = "Tags",
            IsOptional = true,
            HelpText = "Comma separated list of issue tags to apply to item in DevOps")]
        public string Tags { get; set; }

        [SeqAppSetting(
            DisplayName = "Area Path",
            IsOptional = true,
            HelpText = "Area Path of DevOps item")]
        public string AreaPath { get; set; }

        [SeqAppSetting(
            DisplayName = "Iteration",
            IsOptional = true,
            HelpText = "Iteration of the DevOps item")]
        public string Iteration { get; set; }

        [SeqAppSetting(
            DisplayName = "Assigned To",
            IsOptional = true,
            HelpText = "Who the work item should be assigned to. If left blank it will default to unassigned")]
        public string AssignedTo { get; set; }

        [SeqAppSetting(
            DisplayName = "Seq Event Id custom field # within DevOps",
            IsOptional = true,
            HelpText = "Azure DevOps custom field to store Seq Event Id. If provided will be used to prevent duplicate issue creation")]
        public string SeqEventField { get; set; }

        [SeqAppSetting(
            DisplayName = "Issue type",
            HelpText = "DevOps issue type. Possible values: Task, Bug")]
        public string DevOpsIssueType { get; set; }

        [SeqAppSetting(
            DisplayName = "Parent Link URL",
            IsOptional = true,
            HelpText = "Link to the parent related work item. Example: https://yoursite.visualstudio.com/{yourproject}/_workitems/edit/7494. This is useful if you want to make sure all items sent from Seq show up in the same Requirement bucket in CMMI or a product backlog item when using Scrum. If not defined it will be unparented.")]
        public string ParentWorkItemLinkUrl { get; set; }

        //See here for out of box field names: https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/fields/list?view=azure-devops-rest-5.0
        [SeqAppSetting(
            DisplayName = "Seq to DevOps property mapping",
            IsOptional = true,
            HelpText = "Maps Seq properties to DevOps properties. Format: SeqProperty:DevOpsProperty. " +
                       "Separated by Commas. " +
                       "Example: Application:Microsoft.VSTS.Build.FoundIn,MachineName:Microsoft.VSTS.TCM.SystemInfo")]
        public string SeqToDevOpsMapping { get; set; }

        [SeqAppSetting(
            DisplayName = "DevOps property mappings",
            IsOptional = true,
            HelpText = "Maps DevOps properties to statically defined values. Format: DevOpsProperty:StaticValue " +
                       "Separated by Commas. " +
                       "Example: Priority:2,Triage:Level 1")]
        public string DevOpsMappings { get; set; }

        [SeqAppSetting(
            DisplayName = "Debug Mode",
            IsOptional = true,
            InputType = SettingInputType.Checkbox,
            HelpText = "Logs debugging statements to Seq")]
        public bool DebugMode { get; set; }

        #endregion //Settings

        private string _step = "";
        private const uint AlertEventType = 0xA1E77000;

        public async Task OnAsync(Event<LogEventData> evt)
        {
            //If the event level is defined and it is not in the list do not log it
            if ((LogEventLevelList?.Count ?? 0) > 0 && !LogEventLevelList.Contains(evt.Data.Level))
                return;

            try
            {
                await CreateIssueAsync(evt);
            }
            catch (AggregateException aex)
            {
                var fex = aex.Flatten();
                throw new SeqAppException($"Error while creating item in Azure DevOps. The step is: {_step}.", fex);
            }
            catch (Exception ex)
            {
                throw new SeqAppException($"Error while creating item in Azure DevOps. The step is: {_step}.", ex);
            }
        }

        private async Task CreateIssueAsync(Event<LogEventData> evt)
        {
            BeginStep("Connecting to Azure DevOps");

            var connection = new VssConnection(new Uri(AzureDevOpsUrl),
                new VssBasicCredential(string.Empty, PersonalAccessToken));

            var workItemClient = await connection.GetClientAsync<WorkItemTrackingHttpClient>();

            // Try to match an existing work item
            if (!string.IsNullOrEmpty(SeqEventField))
            {
                BeginStep("Querying existing work item");
                var wiql = new Wiql
                {
                    Query = "Select [State], [Title] " +
                            "From WorkItems " +
                            "Where [" + SeqEventField + "] = '" + evt.Id + "' " +
                            "And [System.TeamProject] = '" + Project + "' " +
                            //"And [System.State] <> 'Closed' " +
                            "Order By [State] Asc, [Changed Date] Desc"
                };

                //execute the query to get the list of work items in the results
                var workItemQueryResult = await workItemClient.QueryByWiqlAsync(wiql);

                if (workItemQueryResult.WorkItems.Count() != 0)
                {
                    Log.Information("Duplicate DevOps item creation prevented for event id {id}", evt.Id);
                    return;
                }
            }

            BeginStep("Adding fields");
            var document = new JsonPatchDocument();

            BeginStep("Adding title");
            //DevOps has max 255 character length for title
            var title = $"SEQ Event - {evt.Data.RenderedMessage}".TruncateWithEllipsis(255);
            if (!string.IsNullOrEmpty(Title)) //User has defined their own title parsing
            {
                title = GetSeqMappedPropertyString(Title, evt).TruncateWithEllipsis(255);
            }

            document.Add(
                new JsonPatchOperation
                {
                    Path = "/fields/System.Title",
                    Operation = Operation.Add,
                    Value = title
                });

            document.Add(
                new JsonPatchOperation
                {
                    Path = "/fields/System.AssignedTo",
                    Operation = Operation.Add,
                    Value = AssignedTo ?? string.Empty
                });

            if (!ParentWorkItemLinkUrl.IsNullOrEmpty())
            {
                document.Add(
                    new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = "/relations/-",
                        Value = new
                        {
                            rel = "System.LinkTypes.Hierarchy-Reverse",
                            url = ParentWorkItemLinkUrl,
                            attributes = new
                            {
                                comment = "Seq Event Auto-Parent Link"
                            }
                        }
                    }
                );
            }

            if (!string.IsNullOrEmpty(SeqEventField))
            {
                BeginStep("Adding Seq ID mapping");
                document.Add(new JsonPatchOperation()
                {
                    Path = "/fields/" + SeqEventField,
                    Operation = Operation.Add,
                    Value = evt.Id
                });
            }

            if (!string.IsNullOrEmpty(Tags))
            {
                BeginStep("Adding tags");
                document.Add(new JsonPatchOperation()
                {
                    Path = "/fields/Tags",
                    Operation = Operation.Add,
                    Value = Tags //DevOps takes a comma separated list without any alterations
                });
            }

            BeginStep("Setting description");

            document.Add(
                new JsonPatchOperation()
                {
                    Path = $"/fields/{DescriptionDevOpsMappingField}",
                    Operation = Operation.Add,
                    Value = RenderDescription(evt)
                });

            if (!string.IsNullOrEmpty(SeqToDevOpsMapping))
            {
                BeginStep("Setting Seq to DevOps property mappings");
                var keyValuePairs = SeqToDevOpsMapping.ParseKeyValueArray();
                foreach (var value in keyValuePairs)
                {
                    if (evt.Data.Properties.ContainsKey(value.Key))
                    {
                        LogIfDebug("Setting Seq to DevOps Property: " + value.Value + " Value: " + value.Key);
                        document.Add(
                            new JsonPatchOperation
                            {
                                Path = $"/fields/{value.Value}",
                                Operation = Operation.Add,
                                Value = evt.Data.Properties[value.Key]
                            });
                    }
                }
            }

            if (!string.IsNullOrEmpty(DevOpsMappings))
            {
                BeginStep("Setting DevOps static property mappings");
                var keyValuePairs = DevOpsMappings.ParseKeyValueArray();
                foreach (var value in keyValuePairs)
                {
                    LogIfDebug("Setting DevOps Static Property: {Property} Value: {Value}", value.Key, value.Value);
                    document.Add(
                        new JsonPatchOperation
                        {
                            Path = $"/fields/{value.Key}",
                            Operation = Operation.Add,
                            Value = value.Value
                        });
                }
            }

            if (!string.IsNullOrEmpty(AreaPath))
            {
                BeginStep("Setting Area Path");
                document.Add(
                    new JsonPatchOperation
                    {
                        Path = "/fields/System.AreaPath",
                        Operation = Operation.Add,
                        Value = AreaPath
                    });
            }

            if (!string.IsNullOrEmpty(Iteration))
            {
                BeginStep("Setting Iteration");
                document.Add(
                    new JsonPatchOperation
                    {
                        Path = "/fields/System.IterationPath",
                        Operation = Operation.Add,
                        Value = Iteration
                    });
            }

            BeginStep("Adding work item");
            _ = await workItemClient.CreateWorkItemAsync(document, Project, DevOpsIssueType, false, true);

            BeginStep("Finished process");
        }

        private void BeginStep(string step)
        {
            _step = step;
            LogIfDebug("Step {Step}", step);
        }

        private void LogIfDebug(string messageTemplate, params object[] args)
        {
            if (DebugMode)
            {
                Log.Debug(messageTemplate, args);
            }
        }

        private string RenderDescription(Event<LogEventData> evt)
        {
            if (evt == null)
                return "";

            if (!string.IsNullOrEmpty(Message))
            {
                return GetSeqMappedPropertyString(Message, evt);
            }

            var sb = new StringBuilder();
            if (IsAlert(evt))
            {
                var dashboardUrl = SafeGetProperty(evt, "DashboardUrl");
                var dashboardTitle = SafeGetProperty(evt, "DashboardTitle");
                var chartTitle = SafeGetProperty(evt, "ChartTitle");
                var ownerNamespace = "";
                if (evt.Data.Properties.TryGetValue("OwnerUsername", out var ownerUsernameProperty) &&
                    ownerUsernameProperty is string ownerUsername)
                {
                    if (!string.IsNullOrEmpty(ownerUsername))
                        ownerNamespace = ownerUsername + "/";
                }

                sb.AppendFormat("<strong>Alert Event Id:</strong> {0}<br/>", evt.Id);
                sb.Append($"<strong>Dashboard URL:</strong> <a href=\"{dashboardUrl}\" target=\"_blank\">{ownerNamespace}{dashboardTitle}/{chartTitle}</a><br/>");
            }
            else
                sb.AppendFormat("<strong>Event Id:</strong> {0}<br/>", evt.Id);

            sb.AppendFormat("<strong>Level:</strong> {0}<br/>", evt.Data.Level.ToString());
            sb.AppendFormat("<strong>Timestamp:</strong> {0}<br/>", evt.Data.LocalTimestamp.ToLocalTime());
            sb.Append(
                $"<strong>Event Url:</strong> <a href=\"{GetSeqUrl(evt)}\" target=\"_blank\">Seq Event Url</a><br/>");

            foreach (var m in evt.Data.Properties.Keys)
            {
                LogIfDebug($"Seq Property {m} value: {evt.Data.Properties[m]}");
                sb.Append($"<strong>{m}</strong>: {evt.Data.Properties[m]} <br/>");
            }

            sb.Append($"<strong>Message:</strong> {evt.Data.RenderedMessage}<br/>");

            if ((evt.Data?.Exception ?? "").HasValue())
                sb.AppendFormat(
                    "<strong>Exception:</strong><p style=\"background-color: #921b3c; color: white; border-left: 8px solid #7b1e38;\">{0}</p>",
                    evt.Data.Exception);
            return sb.ToString();
        }

        private string GetSeqUrl(Event<LogEventData> evt)
        {
            return $"{Host.BaseUri}#/events?filter=@Id%20%3D%20'{evt.Id}'&show=expanded";
        }

        private string GetSeqMappedPropertyString(string messageTemplate, Event<LogEventData> evt)
        {
            Log.BindMessageTemplate(messageTemplate, evt.Data.Properties.Select(p => p.Value).ToArray(),
                out var boundMessageTemplate, out _);

            var sb = new StringBuilder();
            foreach (var tok in boundMessageTemplate.Tokens)
            {
                if (tok is TextToken)
                    sb.Append(tok);
                else
                {
                    if (tok.ToString() == "SeqEventId")
                        sb.Append(evt.Id);
                    if (tok.ToString() == "SeqLevel")
                        sb.Append(evt.Data.Level);
                    if (tok.ToString() == "SeqTimestamp")
                        sb.Append(evt.Data.LocalTimestamp.ToLocalTime());
                    if (tok.ToString() == "SeqEventUrl")
                        sb.Append(GetSeqUrl(evt));
                    if (tok.ToString() == "SeqException")
                    {
                        if ((evt.Data?.Exception ?? "").HasValue())
                            sb.AppendFormat(
                                "<strong>Exception:</strong><p style=\"background-color: #921b3c; color: white; border-left: 8px solid #7b1e38;\">{0}</p>",
                                evt.Data.Exception);
                    }

                    if (tok.ToString() == "SeqPropertiesList")
                    {
                        foreach (var m in evt.Data.Properties.Keys)
                        {
                            sb.Append($"<strong>{m}</strong>: {evt.Data.Properties[m]} <br/>");
                        }
                    }
                    else
                        sb.Append(evt.Data.Properties[tok.ToString().Replace("{", "").Replace("}", "")]);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Dashboard alerts create a "virtual" event id, so it doesn't actually point to a specific log, but potentially a set of log events
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        private static bool IsAlert(Event<LogEventData> evt)
        {
            return evt.EventType == AlertEventType;
        }

        private static string SafeGetProperty(Event<LogEventData> evt, string propertyName)
        {
            if (evt.Data.Properties.TryGetValue(propertyName, out var value))
            {
                if (value == null) return "`null`";
                return value.ToString();
            }

            return "";
        }
    }
}
