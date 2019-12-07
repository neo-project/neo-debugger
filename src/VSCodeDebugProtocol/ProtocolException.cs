using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol
{
    [Serializable]
    public class ProtocolException : Exception
    {
        public Message ProtocolMessage
        {
            get;
            private set;
        }

        public int ErrorCode
        {
            get;
            private set;
        }

        public string DetailMessage
        {
            get;
            private set;
        }

        public string FormatString
        {
            get;
            private set;
        }

        public IReadOnlyDictionary<string, object> Variables
        {
            get;
            private set;
        }

        public ProtocolException()
        {
        }

        public ProtocolException(string message)
            : this(message, (Message)null)
        {
        }

        public ProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public ProtocolException(string message, int id, string format, Dictionary<string, object> variables = null, bool? sendTelemetry = null, bool? showUser = null, string url = null, string urlLabel = null)
            : this(message, new Message(id, format)
            {
                Variables = variables,
                SendTelemetry = sendTelemetry,
                ShowUser = showUser,
                Url = url,
                UrlLabel = urlLabel
            })
        {
        }

        public ProtocolException(string message, Message detailMessage)
            : base(message)
        {
            if (detailMessage != null)
            {
                ProtocolMessage = detailMessage;
                ErrorCode = detailMessage.Id;
                DetailMessage = ProtocolUtilities.ExpandVariables(detailMessage.Format, detailMessage.Variables, underscoredOnly: false);
                FormatString = detailMessage.Format;
                Variables = detailMessage.Variables;
            }
        }

        protected ProtocolException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder(Message);
            if (!string.IsNullOrEmpty(DetailMessage))
            {
                stringBuilder.Append(" (");
                stringBuilder.Append(DetailMessage);
                stringBuilder.Append(")");
            }
            return stringBuilder.ToString();
        }
    }
}
