using Hangfire.Server;

using System.Diagnostics;

namespace Altinn.Broker.Integrations.Hangfire;

public class HangfireAppRequestFilter() : IServerFilter
{
    private static readonly AsyncLocal<Activity> _hangfireActivity = new();
    private static readonly ActivitySource _activitySource = new("Altinn.Broker.Integrations.Hangfire");

    public void OnPerformed(PerformedContext context)
    {
        _hangfireActivity.Value?.Stop();
    }

    public void OnPerforming(PerformingContext context)
    {
        var operationName = $"HANGFIRE {context.BackgroundJob.Job.Method.DeclaringType?.Name}.{context.BackgroundJob.Job.Method.Name}";

        var activity = _activitySource.StartActivity(operationName, ActivityKind.Server);
        if (activity != null)
        {
            activity.SetTag("hangfire.job.id", context.BackgroundJob.Id);
            activity.SetTag("hangfire.job.type", context.BackgroundJob.Job.Method.DeclaringType?.Name);
            activity.SetTag("hangfire.job.method", context.BackgroundJob.Job.Method.Name);
            activity.SetTag("hangfire.job.queue", context.BackgroundJob.Job.Queue);
            activity.SetTag("hangfire.job.created_at", context.BackgroundJob.CreatedAt.ToString("O"));

            // Add attributes that make it look like a request to Application Insights
            activity.SetTag("http.method", "HANGFIRE");
            activity.SetTag("http.status_code", "102");
            activity.SetTag("http.target", context.BackgroundJob.Id);
            activity.SetTag("http.host", "hangfire");
            activity.SetTag("http.flavor", "1.1");

            _hangfireActivity.Value = activity;
        }
    }
}
