using NLog.Common;
using System;
using System.Text;

namespace NLogTarget.Splunk
{
    public static class SplunkAuthTokenResolver
    {
        static Func<string, string> _onObtainAuthToken;
        static readonly object _onObtainAuthTokenLock = new object();

        internal static readonly string NullToken = "~$null$~";

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

        internal static string ObtainAuthToken(string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                return NullToken;
            }

            if (_onObtainAuthToken == null)
            {
                InternalLogger.Error($"Thare are no handlers for {nameof(OnObtainAuthToken)} event");

                return NullToken;
            }

            string authToken = null;

            try
            {
                authToken = _onObtainAuthToken.Invoke(targetName);
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, $"error obtaining auth token for target {targetName}");

                return NullToken;
            }

            if (string.IsNullOrEmpty(authToken))
            {
                InternalLogger.Error($"Unable to obtain auth token for target {targetName}. Please ensure you have an appropriate handler for {nameof(OnObtainAuthToken)} event and that the auth token has a value");

                return NullToken;
            }

            InternalLogger.Info($"auth token obtained for target {targetName}");

            return authToken;
        }
    }
}
