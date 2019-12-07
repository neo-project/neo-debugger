using Newtonsoft.Json;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class SourceResponse : ResponseBody
    {
        [JsonProperty("content")]
        public string Content
        {
            get;
            set;
        }

        [JsonProperty("mimeType", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string MimeType
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public SourceResponse(string content, string mimeType = null)
        {
            Content = content;
            MimeType = mimeType;
        }

        public SourceResponse()
        {
        }

        public SourceResponse(string content)
        {
            Content = content;
        }
    }
}
