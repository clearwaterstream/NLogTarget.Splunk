using NLog.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace NLogTarget.Splunk
{
    public static class SplunkAuthTokenResolver
    {
        static Func<string, string> _onObtainAuthToken;
        static readonly object _onObtainAuthTokenLock = new object();

        public static event Func<string, string> OnObtainAuthToken
        {
            add
            {
                lock (_onObtainAuthTokenLock)
                {
                    _onObtainAuthToken += value;
                }
            }
            remove
            {
                lock (_onObtainAuthTokenLock)
                {
                    _onObtainAuthToken -= value;
                }
            }
        }

        public static string ObtainAuthToken(string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
                throw new ArgumentNullException(nameof(targetName));

            if (_onObtainAuthToken == null)
                throw new NLog.NLogConfigurationException($"Thare are no handlers for {nameof(OnObtainAuthToken)} event");

            var authToken = _onObtainAuthToken.Invoke(targetName);

            if (string.IsNullOrEmpty(authToken))
                throw new NLog.NLogConfigurationException($"Unable to obtain auth token for target {targetName}. Please ensure you have an appropriate handler for {nameof(OnObtainAuthToken)} event");

            InternalLogger.Info($"auth token obtained for target {targetName}");

            return authToken;
        }
    }
}
