using System;
using System.Text;
using System.Collections;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ms_azure_monitor_event_hubs_receiver
{
    class Program
    {
        private const string ehubNamespaceConnectionString = "<EVENT_HUB_NAMESPACE_CONNECTION_STRING>";
        private const string eventHubName = "<EVENT_HUB_NAME>";
        private const string blobStorageConnectionString = "<BLOB_STORAGE_CONNECTION_STRING>";
        private const string blobContainerName = "<BLOB_CONTAINER_NAME>";
        private static string NEWRELIC_LICENSE_KEY = "";
        private static string EXECUTION_RESULT = "Sucess";

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide license key as first parameter");
            }
            else
            {
                NEWRELIC_LICENSE_KEY = args[0];

                // Read from the default consumer group: $Default
                string consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

                // Create a blob container client that the event processor will use 
                BlobContainerClient storageClient = new BlobContainerClient(blobStorageConnectionString, blobContainerName);

                // Create an event processor client to process events in the event hub
                EventProcessorClient processor = new EventProcessorClient(storageClient, consumerGroup, ehubNamespaceConnectionString, eventHubName);

                // Register handlers for processing events and handling errors
                processor.ProcessEventAsync += ProcessEventHandler;
                processor.ProcessErrorAsync += ProcessErrorHandler;

                // Start the processing
                await processor.StartProcessingAsync();

                // Wait for 45 seconds for the events to be processed
                await Task.Delay(TimeSpan.FromSeconds(45));

                // Stop the processing
                await processor.StopProcessingAsync();
            }
        }

        static async Task ProcessEventHandler(ProcessEventArgs eventArgs)
        {
            var i = 0;
            string res = "";

            try
            {
                // Get the body of the event
                var json = Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray());

                JObject o = JObject.Parse(json);
                JArray records = (JArray)o["records"];

                // create list of entries to sent
                var logEntries = "[";

                // go through all records
                foreach (JToken jt in records)
                {
                    string category = (string)jt["category"];

                    // I am only interested in Logs right now
                    if (category != null &&
                        category.Length > 0 &&
                        category.Contains("Logs"))
                    {
                        // convert ISO8601 to UNIX timestamp
                        var time = (string)jt["time"];
                        var date = DateTime.Parse(time, null, System.Globalization.DateTimeStyles.RoundtripKind);
                        var unixTimestamp = (long)(date.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;

                        // get single event
                        var jtString = jt.ToString();
                        // replace time attribute with UNIX timestamp attribute
                        jtString = jtString.Replace("\"time\": \"" + time + "\"", "\"timestamp\": \"" + unixTimestamp + "\"");

                        // add event to list
                        logEntries += jtString + ",";

                        i++;
                    }
                }

                // complete and clean-up list
                logEntries += "]";
                logEntries = logEntries.Replace(",]", "]");

                // send data to New Relic using Log API
                var data = new StringContent(logEntries);

                data.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var url = "https://log-api.newrelic.com/log/v1";
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-License-Key", NEWRELIC_LICENSE_KEY);
                var response = await client.PostAsync(url, data);

                string result = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                EXECUTION_RESULT = "Exception: " + ex.Message + "; " + ex.StackTrace;
                res = EXECUTION_RESULT;
            }

            // write output as summary
            EXECUTION_RESULT = "Successfully processed " + i.ToString() + " events. ";
            Console.WriteLine(EXECUTION_RESULT);
            Console.WriteLine(res);

            // Update checkpoint in the blob storage so that the app receives only new events the next time it's run
            await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
        }

        static Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
        {
            // Write details about the error to the console window
            Console.WriteLine($"\tPartition '{ eventArgs.PartitionId}': an unhandled exception was encountered. This was not expected to happen.");
            Console.WriteLine(eventArgs.Exception.Message);
            return Task.CompletedTask;
        }
    }
}
