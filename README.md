# Microsoft Azure Monitor Event Hubs Receiver

Azure Monitor is based on a common monitoring data platform that includes Logs and Metrics. Monitoring data may also be sent to other locations to support certain scenarios such as overall observability use cases that may be required for hybrid, i.e. on-prem and public cloud, or multi-cloud scenarios.

This [article from Microsoft](https://docs.microsoft.com/en-us/azure/azure-monitor/platform/data-sources) describes the different sources of monitoring data collected by Azure Monitor in addition to the monitoring data created by Azure resources. Links are provided to detailed information on configuration required to collect this data to different locations.

## Microsoft Azure Event Hub

One options it to stream Azure Monitor logs and/or metrics to other locations using Event Hubs. [This turorial](Tutorial: Stream Azure Active Directory logs to an Azure event hub) describes in detail how you can stream Azure logs and metrics to an Azure Event Hub.

## Stream data to New Relic Telemetry Data Platform

New Relic's [Telemetry Data Platform](https://newrelic.com/platform/telemetry-data-platform) provides an open telemetry target for all your telemetry data such as Metrics, Events, Logs and Traces (MELT).

From Microsoft Azure Event Hub, you can gather metrics and logs from the Event Hubs API (see this guide on [Getting started receiving messages from an event hub](https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-dotnet-standard-getstarted-send)).

## Microsoft Azure Monitor Event Hubs Receiver for New Relic Logs

Both options below require a New Relic account and its corresponding license key to be provided. New Relic License Key (part of the [account set-up](https://docs.newrelic.com/docs/accounts/accounts-billing/account-setup/new-relic-license-key); New Relic provides a perpetually [free New Relic account](https://newrelic.com/signup)).

### Azure Function

The azure-function folder contains an example to retrieve Azure Monitor Event Hub events through an Azure Function and forward these events to New Relic Logs.

This Azure Function requires an environment variable to be set:

```
NEWRELIC_LICENSE_KEY
```

### Console App

This repository provides an easy way to retrieve events from Azure Event Hub and forward into New Relic's Telemetry Data Platform. The current release focuses on Log data since most of the other Metrics are already captured through [New Relic Azure integrations](https://docs.newrelic.com/docs/integrations/microsoft-azure-integrations/getting-started/introduction-azure-monitoring-integrations).

This console app can easily be configured by providing the following information (a future release will try to gather these from parameters):
- EVENT_HUB_NAMESPACE_CONNECTION_STRING
- EVENT_HUB_NAME
- BLOB_STORAGE_CONNECTION_STRING
- BLOB_CONTAINER_NAME

When executing the console app, you will need to provide a New Relic License Key to pass in as an parameter to the console app. 

You can then execte this app by using this syntax:

```bash
dotnet <PATH-TO-FILE>/ms_azure_monitor_event_hubs_receiver.dll <NEWRELIC_LICENSE_KEY>
```

You can either run this console up in a continuous fashion or scheduler. However, another great way to continuously gather metrics and logs from Azure Event Hub is through integration this console app as a [New Relic Flex](https://github.com/newrelic/nri-flex) integration. A sample config is already available [here](https://github.com/harrykimpel/nri-flex/blob/master/examples/microsoft-azure-monitor-logs.yml).

## Analyze and observe data in New Relic platform

Once the data has been retrieved, you can then analyze the data using New Relic Telemetry Data PLatform or build custom dashboards. An example quere for Azure Function Logs is provided here:

```SQL
SELECT * FROM Log where category = 'FunctionAppLogs' SINCE today
```
