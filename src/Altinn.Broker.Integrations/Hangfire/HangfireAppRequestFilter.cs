﻿using Hangfire.Server;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Altinn.Broker.Integrations.Hangfire;

public class HangfireAppRequestFilter(TelemetryClient telemetryClient) : IServerFilter
{
    private static readonly AsyncLocal<IDisposable> _contextualLogger = new();
    private static readonly AsyncLocal<IOperationHolder<RequestTelemetry>> _hangfireAppRequestLogger = new();

    public void OnPerformed(PerformedContext context)
    {
        telemetryClient.StopOperation(_hangfireAppRequestLogger.Value);
        _contextualLogger.Value?.Dispose();
    }

    public void OnPerforming(PerformingContext context)
    {
        var operationName = new RequestTelemetry
        {
            Name = $"HANGFIRE {context.BackgroundJob.Job.Method.DeclaringType?.Name}.{context.BackgroundJob.Job.Method.Name}"
        };
        _hangfireAppRequestLogger.Value = telemetryClient.StartOperation(operationName);
        _contextualLogger.Value = Serilog.Context.LogContext.PushProperty("JobId", context.BackgroundJob.Id, true);
    }
}
