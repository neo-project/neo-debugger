using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages
{
    public class ExceptionDetails : DebugType
    {
        [JsonProperty("message", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Message
        {
            get;
            set;
        }

        [JsonProperty("typeName", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string TypeName
        {
            get;
            set;
        }

        [JsonProperty("fullTypeName", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FullTypeName
        {
            get;
            set;
        }

        [JsonProperty("evaluateName", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string EvaluateName
        {
            get;
            set;
        }

        [JsonProperty("stackTrace", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string StackTrace
        {
            get;
            set;
        }

        [JsonProperty("innerException", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<ExceptionDetails> InnerException
        {
            get;
            set;
        }

        [JsonProperty("formattedDescription", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FormattedDescription
        {
            get;
            set;
        }

        [JsonProperty("hresult", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int? HResult
        {
            get;
            set;
        }

        [JsonProperty("source", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Source
        {
            get;
            set;
        }

        [Obsolete("This constructor contains optional parameters and is no longer supported.  Please use the constructor which includes only required parameters.")]
        public ExceptionDetails(string message = null, string typeName = null, string fullTypeName = null, string evaluateName = null, string stackTrace = null, List<ExceptionDetails> innerException = null, string formattedDescription = null, int? hresult = null, string source = null)
        {
            Message = message;
            TypeName = typeName;
            FullTypeName = fullTypeName;
            EvaluateName = evaluateName;
            StackTrace = stackTrace;
            InnerException = innerException;
            FormattedDescription = formattedDescription;
            HResult = hresult;
            Source = source;
        }

        public ExceptionDetails()
        {
            InnerException = new List<ExceptionDetails>();
        }
    }
}
