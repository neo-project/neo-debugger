using Newtonsoft.Json.Linq;

namespace Neo.DebugAdapter
{
    class SequencePoint
    {
        public uint Address;
        public string Document;
        public (int line, int column) Start;
        public (int line, int column) End;

        public static SequencePoint FromJson(JToken json)
        {
            (int, int) ParsePosition(string prefix)
            {
                return (json.Value<int>($"{prefix}-line"), json.Value<int>($"{prefix}-column"));
            }

            return new SequencePoint
            {
                Address = json.Value<uint>("address"),
                Document = json.Value<string>("document"),
                Start = ParsePosition("start"),
                End = ParsePosition("end")
            };
        }
    }
}
