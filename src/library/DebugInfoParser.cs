using NeoDebug.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace NeoDebug
{
    internal static class DebugInfoParser
    {
        private static (string, string) SplitComma(string value)
        {
            var values = value.Split(',');
            Debug.Assert(values.Length == 2);
            return (values[0], values[1]);
        }

        static Lazy<Regex> spRegex = new Lazy<Regex>(() => new Regex(@"^(\d+)\[(\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$"));

        private static DebugInfo Parse(JObject json)
        {
            static EventDebugInfo ParseEvent(JToken token)
            {
                var (ns, name) = SplitComma(token.Value<string>("name"));
                var @params = token["params"].Select(t => SplitComma(t.Value<string>()));
                return new EventDebugInfo()
                {
                    Id = token.Value<string>("id"),
                    Name = name,
                    Namespace = ns,
                    Parameters = @params.ToList()
                };
            }

            static SequencePoint ParseSequencePoint(string value, IList<string> documents)
            {
                var matches = spRegex.Value.Match(value);
                Debug.Assert(matches.Groups.Count == 7);

                int ParseGroup(int i)
                {
                    return int.Parse(matches.Groups[i].Value);
                }

                return new SequencePoint
                {
                    Address = ParseGroup(1),
                    Document = documents[ParseGroup(2)],
                    Start = (ParseGroup(3), ParseGroup(4)),
                    End = (ParseGroup(5), ParseGroup(6)),
                };
            }

            static MethodDebugInfo ParseMethod(JToken token, IList<string> documents)
            {
                var (ns, name) = SplitComma(token.Value<string>("name"));
                var @params = token["params"].Select(t => SplitComma(t.Value<string>()));
                var variables = token["variables"].Select(t => SplitComma(t.Value<string>()));
                var sequencePoints = token["sequence-points"].Select(t => ParseSequencePoint(t.Value<string>(), documents));
                var range = token.Value<string>("range").Split('-');
                Debug.Assert(range.Length == 2);

                return new MethodDebugInfo()
                {
                    Id = token.Value<string>("id"),
                    Name = name,
                    Namespace = ns,
                    Range = (int.Parse(range[0]), int.Parse(range[1])),
                    Parameters = @params.ToList(),
                    ReturnType = token.Value<string>("return"),
                    Variables = variables.ToList(),
                    SequencePoints = sequencePoints.ToList(),
                };
            }

            var documents = json["documents"].Select(t => t.Value<string>()).ToList();
            var events = json["events"].Select(ParseEvent).ToList();
            var methods = json["methods"].Select(t => ParseMethod(t, documents)).ToList();

            return new DebugInfo
            {
                Entrypoint = json.Value<string>("entrypoint"),
                Methods = methods,
                Events = events,
            };
        }

        private static DebugInfo ParseLegacy(JObject json)
        {
            static EventDebugInfo ParseEvent(JToken token)
            {
                var @params = token["parameters"].Select(t => (t.Value<string>("name"), t.Value<string>("type")));
                return new EventDebugInfo()
                {
                    Id = token.Value<string>("name"),
                    Name = token.Value<string>("display-name"),
                    Namespace = token.Value<string>("namespace"),
                    Parameters = @params.ToList()
                };
            }

            static SequencePoint ParseSequencePoint(JToken token)
            {
                return new SequencePoint
                {
                    Address = token.Value<int>("address"),
                    Document = token.Value<string>("document"),
                    Start = (token.Value<int>("start-line"), token.Value<int>("start-column")),
                    End = (token.Value<int>("end-line"), token.Value<int>("end-column")),
                };
            }

            static MethodDebugInfo ParseMethod(JToken token)
            {
                var @params = token["parameters"].Select(t => (t.Value<string>("name"), t.Value<string>("type")));
                var variables = token["variables"].Select(t => (t.Value<string>("name"), t.Value<string>("type")));
                var sequencePoints = token["sequence-points"].Select(ParseSequencePoint);

                return new MethodDebugInfo()
                {
                    Id = token.Value<string>("name"),
                    Name = token.Value<string>("display-name"),
                    Namespace = token.Value<string>("namespace"),
                    Range = (token.Value<int>("start-address"), token.Value<int>("end-address")),
                    ReturnType = token.Value<string>("return-type"),

                    Parameters = @params.ToList(),
                    Variables = variables.ToList(),
                    SequencePoints = sequencePoints.ToList(),
                };
            }

            var events = json["events"].Select(ParseEvent).ToList();
            var methods = json["methods"].Select(ParseMethod).ToList();

            return new DebugInfo
            {
                Entrypoint = json.Value<string>("entrypoint"),
                Methods = methods,
                Events = events,
            };
        }

        private static DebugInfo Load(Stream stream )
        {
            using var streamReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            var root = JObject.Load(jsonReader);
            return root.ContainsKey("documents") ? Parse(root) : ParseLegacy(root);
        }

        public static DebugInfo Load(string avmFileName)
        {
            var debugJsonFileName = Path.ChangeExtension(avmFileName, ".avmdbgnfo");
            if (File.Exists(debugJsonFileName))
            {
                using var avmDbgNfoFile = ZipFile.OpenRead(debugJsonFileName);
                using var debugJsonStream = avmDbgNfoFile.Entries[0].Open();
                return Load(debugJsonStream);
            }

            debugJsonFileName = Path.ChangeExtension(avmFileName, ".debug.json");
            if (File.Exists(debugJsonFileName))
            {
                using var debugJsonStream = File.OpenRead(debugJsonFileName);
                return Load(debugJsonStream);
            }

            throw new ArgumentException($"{nameof(avmFileName)} debug info file doesn't exist");
        }
    }
}
