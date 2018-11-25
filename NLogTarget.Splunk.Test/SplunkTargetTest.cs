using NLog;
using System;
using Xunit;
using Xunit.Abstractions;

namespace NLogTarget.Splunk.Test
{
    [Collection("Sequential")]
    public class SplunkTargetTest : IClassFixture<LoggerFixture>
    {
        readonly ITestOutputHelper output;
        readonly LoggerFixture loggerFixture;
        readonly Logger logger;

        public SplunkTargetTest(ITestOutputHelper output, LoggerFixture loggerFixture)
        {
            this.output = output;
            this.loggerFixture = loggerFixture;
            logger = loggerFixture.Logger;
        }

        [Fact]
        public void WriteInfo()
        {
            logger.Info("Testing 1");
            logger.Info("Testing 2");

            var le = new LogEventInfo()
            {
                Level = LogLevel.Info,
                Message = "Testing 3"
            };

            le.Properties["some_prop"] = "some prop value";

            logger.Log(le);

            output.WriteLine("entries logged");
        }

        [Fact]
        public void WriteException()
        {
            var divider = 0;

            try
            {
                var calc = 5 / divider;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Testing 4 - error with an exception");

                output.WriteLine("error logged");
            }
        }
    }

    public class LoggerFixture : IDisposable
    {
        public LoggerFixture()
        {
            SplunkAuthTokenResolver.OnObtainAuthToken += OnObtainAuthToken;

            var logFactory = LogManager.LoadConfiguration("NLog.config");

            Logger = logFactory.GetCurrentClassLogger();
        }

        string OnObtainAuthToken(string targetName)
        {
            // test dynamic token retreival

            return null;
        }

        public void Dispose()
        {
            LogManager.Flush(new TimeSpan(0, 0, 30)); // 30 seconds
        }

        public Logger Logger { get; private set; }
    }
}
