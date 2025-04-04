using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Altinn.Broker.API.Helpers;

public class PropertyPropagationEnricher : ILogEventEnricher
{
    private readonly HashSet<string> _propertiesToPropagate;

    public PropertyPropagationEnricher(params string[] propertiesToPropagate)
    {
        _propertiesToPropagate = new HashSet<string>(propertiesToPropagate);
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            if (token is PropertyToken propertyToken &&
                _propertiesToPropagate.Contains(propertyToken.PropertyName))
            {
                if (logEvent.Properties.TryGetValue(propertyToken.PropertyName, out var value))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(propertyToken.PropertyName, value));
                    LogContext.PushProperty(propertyToken.PropertyName, value);
                }
            }
        }

        if (logEvent.Properties.TryGetValue("fileTransferId", out var fileTransferIdValue))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("instanceId", fileTransferIdValue));
            LogContext.PushProperty("instanceId", fileTransferIdValue);
        }
    }
}
