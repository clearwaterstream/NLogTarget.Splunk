using NLog;
using NLogTarget.Splunk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleAppTest.FullFramework
{
    class Program
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            SplunkAuthTokenResolver.OnObtainAuthToken += SplunkAuthTokenResolver_OnObtainAuthToken;

            var le = new LogEventInfo()
            {
                Level = LogLevel.Info,
                Message = "Testing 1"
            };

            le.Properties["some_prop"] = "some prop value";

            logger.Log(le);

            logger.Info("Testing 2");

            logger.Error("Testing 3 - error");

            int num = 0;

            try
            {
                var x = 5 / num;
            }
            catch(Exception ex)
            {
                logger.Error(ex, "Testing 4 - error with an exception");
            }

            Console.Read();
        }

        static string SplunkAuthTokenResolver_OnObtainAuthToken(string targetName)
        {
            if(targetName == "splunk" || targetName == "splunk_wrapped")
            {
                // get auth token from secrets vault

                return "auth token value";
            }

            return null;
        }
    }
}
