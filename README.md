# NLogTarget.Splunk
A simple, extensible Splunk NLog target that facilitates delivery of log entries to Http Event Collector (HEC)  

Tested with .NET Framework 4.7.2 (should work with 4.7.x) and .NET Core 2.1  

Supports sending log entries in async and sync mode with gzip compression enabled. In async mode, the entries are sent in batches.

## Resolving AuthToken

It is highly recommended that the `AuthToken` value is resolved from a secrets vault rather then NLog.config. You can extend this library and write a custom AuthToken resolution method in `InitializeTarget()`. Unfortunately, NLog does not support "MethodPointer" configuration parameter types. 

## Sample NLog.config

The required parameters are

* `endpoint` - HEC URL, such as https://sample.org/services/collector/event
* `authToken` - Authentication token
* `index` - An index to which to send the event logs to
* `source` - Identifies the source of the event logs

Optional parameters are

* `ignoreSSLErrors` - `False` by default. If `True`, ssl errors are ignored when posting to the HEC endpoint
* `timeout` - # of milliseconds to wait before aborting a POST to HEC endpoint. Default is 30000 (30 seconds).

_Keep in mind that the timestamp must be sent along with the log entries. The library will set the timestamp to the current time (`DateTime.Now`) so ensure that the time across your servers is synchronized._

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:schemaLocation="http://www.nlog-project.org/schemas/NLog.xsd NLog.xsd"
      autoReload="false"
      throwExceptions="false"
      internalLogLevel="Off" internalLogFile="C:\logs\nlog_internal.log">
  <extensions>
    <add assembly="NLogTarget.Splunk"/>
  </extensions>
  <targets async="true">
    <target xsi:type="Splunk" name="splunk" endpoint="https://sample.org/services/collector/event" authToken="***" index="sample_index" source="http:your_app">
      <layout xsi:type="JsonLayout" includeAllProperties="true">
        <attribute name="logger" layout="${logger}" />
        <attribute name="severity" layout="${level}" />
        <attribute name="callsite" layout="${callsite:includeSourcePath=false:className=false}" />
        <attribute name="message" layout="${message}" />
        <attribute name="error" layout="${exception:format=ToString}" />
      </layout>
    </target>
  </targets>
  <rules>
    <logger name="*" minlevel="Info" writeTo="Splunk" />
  </rules>
</nlog>
```

[NLog_sample.config](NLogTarget.Splunk/NLog_sample.config)

### - Enjoy Responsibly -
