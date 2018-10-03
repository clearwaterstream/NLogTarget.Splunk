# SplunkNLogTarget
A simple, extensible Splunk NLog target that facilitates delivery of log entries to Http Event Collector (HEC)  

Tested with .NET Framework 4.7.2 (should work with 4.7.x) and .NET Core 2.1  

Supports sending log entries in async and sync mode with gzip compression enabled. In async mode, the entries are sent in batches.

## Why not make a NuGet package?

We need to resolve the `AuthToken` configuration value and in this simple example the value comes from NLog.config. It is recommended that the `AuthToken` value is rather resolved from a secrets vault. Currently, NLog does not support "MethodPointer" parameter types, so one has to extend this library and write a custom AuthToken resolution method in `InitializeTarget()`

## Sample NLog.config

The required parameters are `endpoint`, `authToken`, `index`, and `aource`. Keep in mind that the timestamp must be sent along with the log entries. The library will set the timestamp to the current time (`DateTime.Now`) but this assumes that time across your servers is synchronized. 

[NLog_sample.conig](blob/master/SplunkNLogTarget/NLog_sample.config)
