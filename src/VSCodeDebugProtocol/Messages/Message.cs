using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class Message : DebugType
    {
        [JsonProperty("id")]
        public int Id
        {
            get;
            set;
        }

        [JsonProperty("format")]
        public string Format
        {
            get;
            set;
        }

        [JsonProperty("variables", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, object> Variables
        {
            get;
            set;
        }

        [JsonProperty("sendTelemetry", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? SendTelemetry
        {
            get;
            set;
        }

        [JsonProperty("showUser", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? ShowUser
        {
            get;
            set;
        }

        [JsonProperty("url", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Url
        {
            get;
            set;
        }

        [JsonProperty("urlLabel", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string UrlLabel
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public Message(int id, string format, Dictionary<string, object> variables = null, bool? sendTelemetry = null, bool? showUser = null, string url = null, string urlLabel = null)
        {
            Id = id;
            Format = format;
            Variables = variables;
            SendTelemetry = sendTelemetry;
            ShowUser = showUser;
            Url = url;
            UrlLabel = urlLabel;
        }

        public Message()
        {
            Variables = new Dictionary<string, object>();
        }

        public Message(int id, string format)
        {
            Id = id;
            Format = format;
        }
    }
}
