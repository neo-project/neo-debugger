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
using System.Threading.Tasks;

namespace NeoDebug.Models
{
    static class DebugInfoParser
    {
        class DocumentResolver
        {
            Dictionary<string, string> folderMap;

            public DocumentResolver(IReadOnlyDictionary<string, string> sourceFileMap)
            {
                folderMap = sourceFileMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            public string ResolveDocument(JToken token)
            {
                var document = token.Value<string>();
                if (File.Exists(document))
                    return document;

                foreach (var kvp in folderMap)
                {
                    if (document.StartsWith(kvp.Key))
                    {
                        var mapDocument = Path.Join(kvp.Value, document.Substring(kvp.Key.Length));
                        if (File.Exists(mapDocument))
                        {
                            return mapDocument;
                        }
                    }
                }

                var cwd = Environment.CurrentDirectory;
                var cwdDocument = Path.Join(cwd, Path.GetFileName(document));
                if (File.Exists(cwdDocument))
                {
                    var directoryName = Path.GetDirectoryName(document);
                    if (directoryName != null)
                    {
                        folderMap.Add(directoryName, cwd);
                    }

                    return cwdDocument;
                }

                var folderName = Path.GetFileName(cwd);
                var folderIndex = document.IndexOf(folderName);
                if (folderIndex >= 0)
                {
                    var relPath = document.Substring(folderIndex + folderName.Length);
                    var newPath = Path.GetFullPath(Path.Join(cwd, relPath));

                    if (File.Exists(newPath))
                        return newPath;
                }

                throw new FileNotFoundException($"could not load {document}");
            }
        }

        private static (string, string) SplitComma(string value)
        {
            var values = value.Split(',');
            Debug.Assert(values.Length == 2);
            return (values[0], values[1]);
        }

        static Lazy<Regex> spRegex = new Lazy<Regex>(() => new Regex(@"^(\d+)\[(\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$"));

        private static DebugInfo Parse(JObject json, DocumentResolver documentResolver)
        {
            static DebugInfo.Event ParseEvent(JToken token)
            {
                var (ns, name) = SplitComma(token.Value<string>("name"));
                var @params = token["params"].Select(t => SplitComma(t.Value<string>()));
                return new DebugInfo.Event()
                {
                    Id = token.Value<string>("id"),
                    Name = name,
                    Namespace = ns,
                    Parameters = @params.ToList()
                };
            }

            static DebugInfo.SequencePoint ParseSequencePoint(string value, IList<string> documents)
            {
                var matches = spRegex.Value.Match(value);
                Debug.Assert(matches.Groups.Count == 7);

                int ParseGroup(int i)
                {
                    return int.Parse(matches.Groups[i].Value);
                }

                return new DebugInfo.SequencePoint
                {
                    Address = ParseGroup(1),
                    Document = documents[ParseGroup(2)],
                    Start = (ParseGroup(3), ParseGroup(4)),
                    End = (ParseGroup(5), ParseGroup(6)),
                };
            }

            static DebugInfo.Method ParseMethod(JToken token, IList<string> documents)
            {
                var (ns, name) = SplitComma(token.Value<string>("name"));
                var @params = token["params"].Select(t => SplitComma(t.Value<string>()));
                var variables = token["variables"].Select(t => SplitComma(t.Value<string>()));
                var sequencePoints = token["sequence-points"].Select(t => ParseSequencePoint(t.Value<string>(), documents));
                var range = token.Value<string>("range").Split('-');
                Debug.Assert(range.Length == 2);

                return new DebugInfo.Method()
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

            var documents = json["documents"].Select(documentResolver.ResolveDocument).ToList();
            var events = json["events"].Select(ParseEvent).ToList();
            var methods = json["methods"].Select(t => ParseMethod(t, documents)).ToList();

            return new DebugInfo
            {
                Entrypoint = json.Value<string>("entrypoint"),
                Methods = methods,
                Events = events,
            };
        }

        private static DebugInfo ParseLegacy(JObject json, DocumentResolver documentResolver)
        {
            static DebugInfo.Event ParseEvent(JToken token)
            {
                var @params = token["parameters"].Select(t => (t.Value<string>("name"), t.Value<string>("type")));
                return new DebugInfo.Event()
                {
                    Id = token.Value<string>("name"),
                    Name = token.Value<string>("display-name"),
                    Namespace = token.Value<string>("namespace"),
                    Parameters = @params.ToList()
                };
            }

            static DebugInfo.SequencePoint ParseSequencePoint(JToken token, DocumentResolver documentResolver)
            {
                return new DebugInfo.SequencePoint
                {
                    Address = token.Value<int>("address"),
                    Document = documentResolver.ResolveDocument(token.Value<string>("document")),
                    Start = (token.Value<int>("start-line"), token.Value<int>("start-column")),
                    End = (token.Value<int>("end-line"), token.Value<int>("end-column")),
                };
            }

            static DebugInfo.Method ParseMethod(JToken token, DocumentResolver documentResolver)
            {
                var @params = token["parameters"].Select(t => (t.Value<string>("name"), t.Value<string>("type")));
                var variables = token["variables"].Select(t => (t.Value<string>("name"), t.Value<string>("type")));
                var sequencePoints = token["sequence-points"].Select(sp => ParseSequencePoint(sp, documentResolver));

                return new DebugInfo.Method()
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
            var methods = json["methods"].Select(m => ParseMethod(m, documentResolver)).ToList();

            return new DebugInfo
            {
                Entrypoint = json.Value<string>("entrypoint"),
                Methods = methods,
                Events = events,
            };
        }

        private static async Task<DebugInfo> Load(Stream stream, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            var documentResolver = new DocumentResolver(sourceFileMap);
            using var streamReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            var root = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            return root.ContainsKey("documents") ? Parse(root, documentResolver) : ParseLegacy(root, documentResolver);
        }

        public static async Task<DebugInfo> Load(string avmFileName, IReadOnlyDictionary<string, string> sourceFileMap)
        {
            var debugJsonFileName = Path.ChangeExtension(avmFileName, ".avmdbgnfo");
            if (File.Exists(debugJsonFileName))
            {
                using var avmDbgNfoFile = ZipFile.OpenRead(debugJsonFileName);
                using var debugJsonStream = avmDbgNfoFile.Entries[0].Open();
                return await Load(debugJsonStream, sourceFileMap).ConfigureAwait(false);
            }

            debugJsonFileName = Path.ChangeExtension(avmFileName, ".debug.json");
            if (File.Exists(debugJsonFileName))
            {
                using var debugJsonStream = File.OpenRead(debugJsonFileName);
                return await Load(debugJsonStream, sourceFileMap).ConfigureAwait(false);
            }

            throw new ArgumentException($"{nameof(avmFileName)} debug info file doesn't exist");
        }
    }
}
