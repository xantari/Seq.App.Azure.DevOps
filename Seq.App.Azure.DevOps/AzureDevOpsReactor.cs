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
using Newtonsoft;
using Serilog.Events;
using Serilog.Parsing;
using Newtonsoft.Json;

namespace Seq.App.Azure.DevOps
{
    [SeqApp("Azure DevOps",
      Description = "Posts seq event as a work item to Azure DevOps")]
    public class AzureDevOpsReactor : Reactor, ISubscribeTo<LogEventData>
    {
        #region Settings 
        [SeqAppSetting(DisplayName = "Azure DevOps Url",
            IsOptional = false,
             HelpText = "URL of your Azure DevOps Site (Example: https://yoursite.visualstudio.com/ or https://dev.azure.com/{your organization}/).")]
        public string AzureDevOpsUrl { get; set; }

        [SeqAppSetting(DisplayName = "Project",
    IsOptional = false,
     HelpText = "Project Name, this is the {your project} part of your project URL (Example: https://yoursite.visualstudio.com/{your project} or https://dev.azure.com/{your organization}/{your project}).")]
        public string Project { get; set; }

        [SeqAppSetting(DisplayName = "Azure DevOps Personal Access Token",
            IsOptional = false,
             HelpText = "Azure DevOps Personal Access Token.")]
        public string PersonalAccessToken { get; set; }

        [SeqAppSetting(DisplayName = "Comma seperated list of event levels",
            IsOptional = true,
            HelpText = "If specified Azure DevOps issue (work item or bug) will be created only for the specified event levels, other levels will be discarded")]
        public string LogEventLevels { get; set; }

        public List<Apps.LogEvents.LogEventLevel> LogEventLevelList
        {
            get
            {
                List<Apps.LogEvents.LogEventLevel> result = new List<Apps.LogEvents.LogEventLevel>();
                if (string.IsNullOrEmpty(LogEventLevels))
                    return result;

                var strValues = LogEventLevels.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if ((strValues?.Length ?? 0) == 0)
                    return result;

                strValues.Aggregate(result, (acc, strValue) =>
                {
                    Apps.LogEvents.LogEventLevel enumValue = Apps.LogEvents.LogEventLevel.Debug;
                    if (Enum.TryParse<Apps.LogEvents.LogEventLevel>(strValue, out enumValue))
                        acc.Add(enumValue);
                    return acc;
                });

                return result;
            }
        }

        [SeqAppSetting(DisplayName = "Title",
        HelpText = "Title of created Azure DevOps items. Use {PropertyName} to insert properties into the field. Ex. {Application}, {Message}, etc. If not defined with will follow the format: Seq Event - {message}. Max Length is 255. The following special properties are available: SeqEventId, SeqLevel, SeqTimestamp, SeqEventUrl",
        IsOptional = true)]
        public string Title { get; set; }

        [SeqAppSetting(DisplayName = "Description",
            InputType = SettingInputType.LongText,
            HelpText = "Description of created Azure DevOps items. Use {PropertyName} to insert properties into the field. Ex. {Application}, {Message}, etc. If not defined with will output all properties and exception data from Seq in a pretty format. The following special properties are available: SeqEventId, SeqLevel, SeqTimestamp, SeqEventUrl",
            IsOptional = true)]
        public string Message { get; set; }

        //Microsoft.VSTS.CMMI.Symptom
        [SeqAppSetting(DisplayName = "Description Mapping Field",
            HelpText = "Description DevOps Mapping Field. For Bugs using CMMI this typically be Microsoft.VSTS.CMMI.Symptom, For CMMI Tasks it would be: System.Description. For Bugs in Scrumm you might use Repro Steps: Microsoft.VSTS.TCM.ReproSteps",
            IsOptional = false)]
        public string DescriptionDevOpsMappingField { get; set; }

        [SeqAppSetting(
            DisplayName = "Tags",
            IsOptional = true,
            HelpText = "Comma seperated list of issue tags to apply to item in DevOps")]
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
            DisplayName = "Seq Event Id custom field # within DevOps",
            IsOptional = true,
            HelpText = "Azure DevOps custom field to store Seq Event Id. If provided will be used to prevent duplicate issue creation")]
        public int? SeqEventField { get; set; }

        [SeqAppSetting(
            DisplayName = "Issue type",
            IsOptional = false,
            HelpText = "DevOps issue type. Possible values: Task, Bug")]
        public string DevOpsIssueType { get; set; }

        //See here for out of box field names: https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/fields/list?view=azure-devops-rest-5.0
        [SeqAppSetting(
            DisplayName = "Seq to DevOps property mapping",
            IsOptional = true,
            HelpText = "Maps Seq properties to DevOps properties. Format: SeqProperty:DevOpsProperty. " +
            "Seperated by Commas. " +
            "Example: Application:Microsoft.VSTS.Build.FoundIn,MachineName:Microsoft.VSTS.TCM.SystemInfo")]
        public string SeqToDevOpsMapping { get; set; }

        [SeqAppSetting(
            DisplayName = "DevOps property mappings",
            IsOptional = true,
            HelpText = "Maps DevOps properties to staticly defined values. Format: DevOpsProperty:StaticValue " +
            "Seperated by Commas. " +
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

        public void On(Event<LogEventData> evt)
        {
            try
            {
                var result = CreateIssue(evt).Result;
            }
            catch (AggregateException aex)
            {
                var fex = aex.Flatten();
                Log.Error(fex, "Error while creating item in Azure DevOps. Step is: {_step}", _step);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while creating item in Azure DevOps. The step is: {_step}", _step);
            }
        }

        public async Task<bool> CreateIssue(Event<LogEventData> evt)
        {
            if (evt == null) //Not sure when or if this could happen?
                return false;

            //If the event level is defined and it is not in the list do not log it
            if ((LogEventLevelList?.Count ?? 0) > 0 && !LogEventLevelList.Contains(evt.Data.Level))
                return false;

            _step = "Beginning process";
            LogIfDebug(_step);
            var description = evt.Data.Exception ?? evt.Data.RenderedMessage;
            //var messageId = ComputeId(description);

            var connection = new VssConnection(new Uri(AzureDevOpsUrl), new VssBasicCredential(string.Empty, PersonalAccessToken));

            var workitemClient = await connection.GetClientAsync<WorkItemTrackingHttpClient>();

            WorkItemQueryResult workItemQueryResult = null;
            // Try to match an existing work item
            if (SeqEventField.HasValue)
            {
                _step = "Querying existing work item";
                LogIfDebug(_step);
                Wiql wiql = new Wiql()
                {
                    Query = "Select [State], [Title] " +
                "From WorkItems " +
                "Where [" + SeqEventField + "] = '" + evt.Id + "' " +
                "And [System.TeamProject] = '" + Project + "' " +
                //"And [System.State] <> 'Closed' " +
                "Order By [State] Asc, [Changed Date] Desc"
                };

                //execute the query to get the list of work items in teh results
                workItemQueryResult = await workitemClient.QueryByWiqlAsync(wiql);

                if (workItemQueryResult.WorkItems.Count() == 0)
                {
                    Log.Information("Duplicate DevOps item creation prevented for event id {id}", evt.Id);
                    return false;
                }
            }

            _step = "Adding fields";
            LogIfDebug(_step);
            var document = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument();

            _step = "Adding title";
            string title = $"SEQ Event - {evt.Data.RenderedMessage}".TruncateWithEllipsis(255); //DevOps has max 255 character length for title
            if (!string.IsNullOrEmpty(Title)) //User has defined their own title parsing
            {
                title = GetSeqMappedPropertyString(Title, evt).TruncateWithEllipsis(255);
            }
            document.Add(
                new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation()
                {
                    Path = "/fields/System.Title",
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Value = title
                });

            if (SeqEventField.HasValue)
            {
                _step = "Adding Seq ID mapping";
                LogIfDebug(_step);
                document.Add(new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation()
                {
                    Path = "/fields/" + SeqEventField,
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Value = evt.Id
                });
            }

            if (!string.IsNullOrEmpty(Tags))
            {
                _step = "Adding tags";
                LogIfDebug(_step);
                document.Add(new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation()
                {
                    Path = "/fields/Tags",
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Value = Tags //DevOps takes a comma seperated list without any alterations
                });
            }

            _step = "Setting description";
            LogIfDebug(_step);

            document.Add(
                new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation()
                {
                    Path = $"/fields/{DescriptionDevOpsMappingField}",
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Value = RenderDescription(evt)
                });

            if (!string.IsNullOrEmpty(SeqToDevOpsMapping))
            {
                _step = "Setting Seq to DevOps property mappings";
                LogIfDebug(_step);
                var keyValuePairs = SeqToDevOpsMapping.ParseKeyValueArray();
                foreach (var value in keyValuePairs)
                {
                    if (evt.Data.Properties.ContainsKey(value.Key))
                    {
                        LogIfDebug("Setting Seq to DevOps Property: " + value.Value + " Value: " + value.Key);
                        document.Add(
                        new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation()
                        {
                            Path = $"/fields/{value.Value}",
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Value = evt.Data.Properties[value.Key]
                        });
                    }
                }
            }

            if (!string.IsNullOrEmpty(DevOpsMappings))
            {
                _step = "Setting DevOps static property mappings";
                LogIfDebug(_step);
                var keyValuePairs = DevOpsMappings.ParseKeyValueArray();
                foreach (var value in keyValuePairs)
                {
                    if (evt.Data.Properties.ContainsKey(value.Key))
                    {
                        LogIfDebug("Setting DevOps Static Property: " + value.Value + " Value: " + value.Key);
                        document.Add(
                        new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation()
                        {
                            Path = $"/fields/{value.Value}",
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Value = evt.Data.Properties[value.Key]
                        });
                    }
                }
            }

            if (!string.IsNullOrEmpty(AreaPath))
            {
                _step = "Setting Area Path";
                LogIfDebug(_step);
                document.Add(
                new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation()
                {
                    Path = $"/fields/System.AreaPath",
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Value = AreaPath
                });
            }

            if (!string.IsNullOrEmpty(Iteration))
            {
                _step = "Setting Iteration";
                LogIfDebug(_step);
                document.Add(
                new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation()
                {
                    Path = $"/fields/System.IterationPath",
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Value = Iteration
                });
            }

            _step = "Adding work item";
            LogIfDebug(_step);
            var workitem = await workitemClient.CreateWorkItemAsync(document, Project, DevOpsIssueType, false, true);

            _step = "Finished process";
            LogIfDebug(_step);
            return true;
        }

        private void LogIfDebug(string message)
        {
            if (DebugMode)
            {
                Log.Debug("{msg}", message);
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
            else
            {
                var sb = new StringBuilder();
                sb.AppendFormat("<strong>Event Id:</strong> {0}<br/>", evt.Id);
                sb.AppendFormat("<strong>Level:</strong> {0}<br/>", evt.Data.Level.ToString());
                sb.AppendFormat("<strong>Timestamp:</strong> {0}<br/>", evt.Data.LocalTimestamp.ToLocalTime());
                var eventUrl = $"{Host.BaseUri}#/events?filter=@Id%20%3D%20'{evt.Id}'";
                sb.Append($"<strong>Event Url:</strong> <a href=\"{eventUrl}\" target=\"_blank\">Seq Event Url</a><br/>");

                foreach (var m in evt.Data.Properties.Keys)
                {
                    sb.Append($"<strong>{m.ToString()}</strong>: {evt.Data.Properties[m]} <br/>");
                }

                sb.Append($"<strong>Message:</strong> {evt.Data.RenderedMessage}<br/>");

                if ((evt?.Data?.Exception ?? "").HasValue())
                    sb.AppendFormat("<strong>Exception:</strong><p style=\"background-color: #921b3c; color: white; border-left: 8px solid #7b1e38;\">{0}</p>", evt.Data.Exception);
                return sb.ToString();
            }
        }

        private string GetSeqMappedPropertyString(string messageTemplate, Event<LogEventData> evt)
        {
            MessageTemplate boundMessageTemplate = null;
            IEnumerable<LogEventProperty> props = new List<LogEventProperty>();
            Log.BindMessageTemplate(messageTemplate, evt.Data.Properties.Select(p => p.Value).ToArray(), out boundMessageTemplate, out props);

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
                        sb.Append($"{Host.BaseUri}#/events?filter=@Id%20%3D%20'{evt.Id}'");
                    else
                        sb.Append(evt.Data.Properties[tok.ToString().Replace("{", "").Replace("}", "")]);
                }
            }
            return sb.ToString();
        }
    }
}