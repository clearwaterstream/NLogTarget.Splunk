/* Igor Krupin
 * https://github.com/clearwaterstream/SplunkNLogTarget
 */
using Newtonsoft.Json;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;

namespace DTCanada.Logging
{
    [Target("Splunk")]
    public class SplunkNLogTarget : TargetWithLayout
    {
        string machineHostAddr = null;
        string channel = null;

        public SplunkNLogTarget()
        {
        }

        public SplunkNLogTarget(string name) : this()
        {
            Name = name;
        }

        static readonly JsonSerializerSettings _logEntrySerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        [RequiredParameter]
        public string Endpoint { get; set; }

        [RequiredParameter]
        public string AuthToken { get; set; }

        [RequiredParameter]
        public string Index { get; set; }

        [RequiredParameter]
        public string Source { get; set; }

        public bool IgnoreSSLErrors { get; set; } = false;

        /// <summary>
        /// Timeout, in milliseconds, after which a POST to HEC will be aborted 
        /// </summary>
        public int Timeout { get; set; } = (int)new TimeSpan(0, 0, 30).TotalMilliseconds;

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var host = Dns.GetHostEntry(Dns.GetHostName());

            var machineIp = host.AddressList.FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            machineHostAddr = machineIp?.ToString();

            channel = Guid.NewGuid().ToString().ToUpperInvariant();

            /* It is highly recommended that you write a function here to resolve the AuthToken from a secure location.
            * Do not store the AuthToken in NLog.config as it may inadvertently may be checked into into your code repository
            */
        }

        protected override void Write(LogEventInfo logEvent)
        {
            InternalLogger.Debug($"Sending to endpoint: {Endpoint}");

            var request = CreateWebRequest();

            using (var reqStream = request.GetRequestStream())
            {
                using (var gzip = new GZipStream(reqStream, CompressionMode.Compress))
                {
                    using (var sw = new StreamWriter(gzip))
                    {
                        using (var jw = new JsonTextWriter(sw))
                        {
                            var ser = JsonSerializer.Create(_logEntrySerializerSettings);

                            SerializeLogEntry(jw, ser, logEvent);
                        }
                    }
                }
            }

            ProcessSplunkResponse(request);
        }

        protected override void Write(IList<AsyncLogEventInfo> logEvents)
        {
            InternalLogger.Debug($"Sending {logEvents.Count} entries to endpoint: {Endpoint}");

            var request = CreateWebRequest();

            using (var reqStream = request.GetRequestStream())
            {
                using (var gzip = new GZipStream(reqStream, CompressionMode.Compress))
                {
                    using (var sw = new StreamWriter(gzip))
                    {
                        using (var jw = new JsonTextWriter(sw))
                        {
                            var ser = JsonSerializer.Create(_logEntrySerializerSettings);

                            foreach (var asyncLogEvent in logEvents)
                            {
                                try
                                {
                                    SerializeLogEntry(jw, ser, asyncLogEvent.LogEvent);

                                    asyncLogEvent.Continuation(null);
                                }
                                catch (Exception ex)
                                {
                                    InternalLogger.Error(ex, "Error writing Splunk log event");

                                    asyncLogEvent.Continuation(ex);
                                }
                            }
                        }
                    }
                }
            }

            ProcessSplunkResponse(request);
        }

        private HttpWebRequest CreateWebRequest()
        {
            var request = WebRequest.CreateHttp(Endpoint);

            request.Method = "POST";
            request.Proxy = null;
            request.KeepAlive = true;
            request.Timeout = Timeout;
            if (IgnoreSSLErrors) // not recommended
                request.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => { return true; };

            request.Headers.Add(HttpRequestHeader.Authorization, $"Splunk {AuthToken}");
            request.Headers.Add(HttpRequestHeader.ContentEncoding, "gzip");
            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            request.Headers.Add("X-Splunk-Request-Channel", channel);

            request.ContentType = "application/json";
            request.Accept = "application/json";

            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            return request;
        }

        static void ProcessSplunkResponse(HttpWebRequest request)
        {
            using (var resp = request.GetResponse())
            {
                using (var rs = resp.GetResponseStream())
                {
                    using (var sr = new StreamReader(rs))
                    {
                        var serverReply = sr.ReadToEnd();

                        InternalLogger.Debug($"Server reply: {serverReply}");

                        // you may want to inspect and handle various server replies from HEC
                    }
                }
            }
        }

        private void SerializeLogEntry(JsonTextWriter jsonWriter, JsonSerializer serializer, LogEventInfo logEvent)
        {
            var splunkLogEvent = new SplunkLogEvent()
            {
                host = $"{Environment.MachineName} {machineHostAddr}",
                index = Index,
                source = Source,
                sourcetype = "_json",
                @event = Layout.Render(logEvent)
            };

            InternalLogger.Debug($"Sending: {splunkLogEvent.@event}");

            serializer.Serialize(jsonWriter, splunkLogEvent);
        }

        public class SplunkLogEvent
        {
            public SplunkLogEvent()
            {
                double epochTime = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

                time = epochTime.ToString("#.000"); // truncate to 3 digits after floating point
            }

            public string time { get; }
            public string index { get; set; }
            public string source { get; set; }
            public string sourcetype { get; set; }
            public string host { get; set; }
            public string @event { get; set; }
        }
    }
}
