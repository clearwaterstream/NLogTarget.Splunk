using System;
using System.Collections.Generic;
using System.Text;

namespace NLogTarget.Splunk
{
    internal class SplunkLogEvent
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
