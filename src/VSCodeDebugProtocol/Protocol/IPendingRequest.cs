using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
    internal interface IPendingRequest
    {
        ProtocolRequest Request
        {
            get;
        }

        bool Cancelled
        {
            get;
        }

        TimeSpan ElapsedTime
        {
            get;
        }

        void InvokeCompletionFunc(object args, JToken responseBody, JsonSerializer serializer);

        void InvokeErrorFunc(object args, ProtocolException ex);

        void Cancel();
    }
}
