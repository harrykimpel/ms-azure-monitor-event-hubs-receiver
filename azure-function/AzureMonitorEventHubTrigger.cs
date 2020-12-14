using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace NewRelic.Function
{
    public static class AzureMonitorEventHubTrigger
    {
        [FunctionName("AzureMonitorEventHubTrigger")]
        public static async Task Run([EventHubTrigger("newrelic", Connection = "<EVENT_HUB_CONNECTION>")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();
            var NEWRELIC_LICENSE_KEY = Environment.GetEnvironmentVariable("NEWRELIC_LICENSE_KEY", EnvironmentVariableTarget.Process);

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    var i = 0;

                    // Get the body of the event
                    var json = Encoding.UTF8.GetString(eventData.Body.Array);

                    JObject o = JObject.Parse(json);
                    JArray records = (JArray)o["records"];

                    // create list of entries to sent
                    var logEntries = "[";

                    // go through all records
                    foreach (JToken jt in records)
                    {
                        string category = (string)jt["category"];
                        string categoryUpp = (string)jt["Category"];

                        // I am only interested in Logs right now
                        if ((category != null &&
                            category.Length > 0 &&
                            category.Contains("Logs"))
                            ||
                            (categoryUpp != null &&
                            categoryUpp.Length > 0 &&
                            categoryUpp.Contains("Logs")))
                        {
                            // convert ISO8601 to UNIX timestamp
                            var time = (string)jt["time"];
                            var date = DateTime.Parse(time, null, System.Globalization.DateTimeStyles.RoundtripKind);
                            var unixTimestamp = (long)(date.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;

                            // get single event
                            var jtString = jt.ToString();

                            // replace time attribute with UNIX timestamp attribute
                            jtString = jtString.Replace("\"time\": \"" + time + "\"", "\"timestamp\": \"" + unixTimestamp + "\"");

                            // set consistent name for category and add logtype
                            if (category != null &&
                                category.Length > 0)
                            {
                                jtString = jtString.Replace("\"category\": \"" + category + "\"", "\"logtype\": \"" + category + "\", \"category\": \"" + category + "\"");
                            }
                            else if (categoryUpp != null &&
                                categoryUpp.Length > 0)
                            {
                                jtString = jtString.Replace("\"Category\": \"" + categoryUpp + "\"", "\"logtype\": \"" + categoryUpp + "\", \"category\": \"" + categoryUpp + "\"");
                            }

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

                    // write output as summary
                    log.LogInformation("Successfully processed " + i.ToString() + " events. ");

                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
