/* Igor Krupin
 * https://github.com/clearwaterstream/NLogTarget.Splunk
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

namespace NLogTarget.Splunk
{
    [Target("Splunk")]
    public class SplunkTarget : TargetWithLayout
    {
        string machineHostAddr = null;
        string channel = null;

        static readonly string _resolveAuthTokenFlag = "*resolve*";

        static readonly Encoding _encoding = new UTF8Encoding(false); // important NOT to send the BOM

        readonly HttpClientHandler _handler;
        readonly HttpClient _client;

        public SplunkTarget()
        {
            _handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _client = new HttpClient(_handler);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.ExpectContinue = false;
            _client.DefaultRequestHeaders.ConnectionClose = false;
        }

        public SplunkTarget(string name) : this()
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

            if (IgnoreSSLErrors) // not recommended
                _handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyError) => true;
        }

        protected override void Write(LogEventInfo logEvent)
        {
            if (!AuthTokenResolved())
            {
                InternalLogger.Warn("auth token not resolved. skipping log entry.");
                return;
            }

            InternalLogger.Debug($"Sending to endpoint: {Endpoint}");

            using (var ms = new MemoryStream())
            {
                using (var gzip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    using (var sw = new StreamWriter(gzip, _encoding, 1024, true))
                    {
                        using (var jw = new JsonTextWriter(sw))
                        {
                            var ser = JsonSerializer.Create(_logEntrySerializerSettings);

                            SerializeLogEntry(jw, ser, logEvent);
                        }
                    }
                }

                ms.Position = 0;

                SendToEventCollector(ms);
            }
        }

        protected override void Write(IList<AsyncLogEventInfo> logEvents)
        {
            if (!AuthTokenResolved())
            {
                InternalLogger.Warn("auth token not resolved. skipping log entry.");
                return;
            }

            InternalLogger.Debug($"Sending {logEvents.Count} entries to endpoint: {Endpoint}");

            using (var ms = new MemoryStream())
            {
                using (var gzip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    using (var sw = new StreamWriter(gzip, _encoding, 1024, true))
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

                ms.Position = 0;

                SendToEventCollector(ms);
            }
        }

        bool AuthTokenResolved()
        {
            if (_resolveAuthTokenFlag.Equals(AuthToken, StringComparison.OrdinalIgnoreCase))
            {
                AuthToken = SplunkAuthTokenResolver.ObtainAuthToken(this.Name);
            }

            if (AuthToken == SplunkAuthTokenResolver.NullToken)
            {
                return false;
            }

            return true;
        }

        void SendToEventCollector(MemoryStream ms)
        {
            using (var reqContent = new StreamContent(ms))
            {
                reqContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                reqContent.Headers.ContentEncoding.Add("gzip");

                using (var reqMsg = new HttpRequestMessage(HttpMethod.Post, Endpoint))
                {
                    reqMsg.Content = reqContent;

                    reqMsg.Headers.Add("Authorization", $"Splunk {AuthToken}");
                    reqMsg.Headers.Add("X-Splunk-Request-Channel", channel);

                    var ct = new CancellationTokenSource(Timeout).Token;

                    using (var response = _client.SendAsync(reqMsg, ct).ConfigureAwait(false).GetAwaiter().GetResult())
                    {
                        var serverReply = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                        InternalLogger.Debug($"Server reply: {serverReply}");

                        // you may want to inspect and handle various server replies from HEC
                    }
                }
            }
        }

        void SerializeLogEntry(JsonTextWriter jsonWriter, JsonSerializer serializer, LogEventInfo logEvent)
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

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                _handler?.Dispose();
                _client?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
