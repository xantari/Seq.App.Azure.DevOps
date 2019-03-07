using System;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Events;

namespace Seq.App.Azure.DevOps.Tests
{
    [TestClass]
    public class AzureDevOpsTests
    {
        private string _uri = "https://testbed.visualstudio.com";
        private string _personalAccessToken = "";
        private string _project = "SEQ Test";

        [TestMethod]
        public void TestWorkItemLookupRetrieval()
        {
            //https://github.com/Microsoft/vsts-dotnet-samples
            //https://github.com/Microsoft/vsts-dotnet-samples/blob/master/ClientLibrary/Snippets/Microsoft.TeamServices.Samples.Client/Work/TeamSettingsSample.cs

            Uri uri = new Uri(_uri);
            string personalAccessToken = _personalAccessToken;
            string project = _project;

            VssBasicCredential credentials = new VssBasicCredential("", _personalAccessToken);

            //create a wiql object and build our query
            Wiql wiql = new Wiql()
            {
                Query = "Select [State], [Title] " +
                        "From WorkItems " +
                        "Where [Work Order ID] = '9999999' " +
                        "And [System.TeamProject] = '" + project + "' " +
                        "And [System.State] <> 'Closed' " +
                        "Order By [State] Asc, [Changed Date] Desc"
            };

            //create instance of work item tracking http client
            using (WorkItemTrackingHttpClient workItemTrackingHttpClient = new WorkItemTrackingHttpClient(uri, credentials))
            {
                //execute the query to get the list of work items in teh results
                WorkItemQueryResult workItemQueryResult = workItemTrackingHttpClient.QueryByWiqlAsync(wiql).Result;
            }
        }

        [TestMethod]
        public void LogLotsOfEventsToTestSeqServer()
        {
            Log.Logger = new LoggerConfiguration()
             .WriteTo.Seq("http://localhost:5341", compact: false) //compact must stay false because seq-forwarder does not support compact mode
                            .CreateLogger();

            for(int i=0; i<= 200; i++)
            {
                Log.Error(new Exception("Test Exception"), "This is a test message. {id}", i);
            }

            Log.CloseAndFlush();
        }
    }
}
