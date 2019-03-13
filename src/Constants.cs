using System;
using System.Collections.Generic;
using System.Text;

namespace ChatContentModerator
{
    class Constants
    {
        // Event Hubs
        public const string EventHubSender = "chatcontentmoderator-sender";
        public const string EventHubReceiver = "chatcontentmoderator-receiver";

        // Cognitive service
        public const string AzureBaseURLWithRegion = "https://westus.api.cognitive.microsoft.com";
    }
}
