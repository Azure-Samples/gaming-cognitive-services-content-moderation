using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.CognitiveServices.ContentModerator;
using Microsoft.Azure.CognitiveServices.ContentModerator.Models;
using Newtonsoft.Json;
using System.IO;

namespace ChatContentModerator
{
    public static class ContentModerator
    {
        [FunctionName("ContentModerator")]
        [return: EventHub(Constants.EventHubReceiver, Connection = "EVENTHUB_CONNECTION_STRING")]
        public static string Run([EventHubTrigger(Constants.EventHubSender, Connection = "EVENTHUB_CONNECTION_STRING")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    Output output = new Output();

                    log.LogInformation($"Autocorrect typos, check for matching terms, PII, and classify for string: {messageBody}");
                    string moderatedString = ModerateString(messageBody, log);

                    if (moderatedString != null)
                    {
                        log.LogInformation($"Moderated string: {moderatedString}");

                        output.ModeratedString = moderatedString;
                        output.OriginalString = messageBody;

                        var outputJson = JsonConvert.SerializeObject(output);

                        return outputJson;
                    }
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

            return null;
        }

        [FunctionName("ModerateString")]
        public static string ModerateString(string input, ILogger log)
        {
            // Your Content Moderator subscription key.
            string CSSubscriptionKey = Environment.GetEnvironmentVariable("CONTENTMODERATOR_KEY");

            ContentModeratorClient client = new ContentModeratorClient(new ApiKeyServiceClientCredentials(CSSubscriptionKey));
            client.Endpoint = Constants.AzureBaseURLWithRegion;

            // Convert string to stream for the content moderator
            byte[] byteArray = Encoding.ASCII.GetBytes(input);
            MemoryStream stream = new MemoryStream(byteArray);

            Screen screenResult;

            try
            {
                screenResult = client.TextModeration.ScreenText("text/plain", stream, null, null, true, null, false);
            }
			catch
			{
                screenResult = null;
            }

            if (screenResult != null)
            {
                string test = JsonConvert.SerializeObject(screenResult);

                log.LogInformation($"{test}");

                // Remove offensive text
                return ReplaceOffensiveStrings(screenResult, screenResult.OriginalText.ToString()); ;
            }

            return null;
        }

        [FunctionName("ReplaceOffensiveStrings")]
        public static string ReplaceOffensiveStrings(Screen moderator, string input)
        {
            string replacement = "***";
            string output = input;

            foreach (var email in moderator.PII.Email)
            {
                output = output.Replace(email.Text, replacement);
            }

            foreach (var ssn in moderator.PII.SSN)
            {
                output = output.Replace(ssn.Text, replacement);
            }

            foreach (var ipa in moderator.PII.IPA)
            {
                output = output.Replace(ipa.Text, replacement);
            }

            foreach (var phone in moderator.PII.Phone)
            {
                output = output.Replace(phone.Text, replacement);
            }

            foreach (var address in moderator.PII.Address)
            {
                output = output.Replace(address.Text, replacement);
            }

            if (moderator.Terms != null)
            {
                foreach (var term in moderator.Terms)
                {
                    output = output.Replace(term.Term, replacement);
                }
            }

            return output;
        }
    }

    class Output
    {
        public string ModeratedString { get; set; }

        public string OriginalString { get; set; }
    }
}
