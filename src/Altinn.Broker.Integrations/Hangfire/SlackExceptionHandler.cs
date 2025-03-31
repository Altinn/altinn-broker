using Microsoft.Extensions.Logging;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Server;
using Altinn.Broker.Integrations.Slack;

namespace Altinn.Broker.Integrations.Hangfire
{
    public class SlackExceptionHandler : JobFilterAttribute, IServerFilter
    {
        private readonly SlackExceptionNotificationHandler _slackExceptionNotification;
        private readonly ILogger<SlackExceptionHandler> _logger;

        public SlackExceptionHandler(SlackExceptionNotificationHandler slackExceptionNotification, ILogger<SlackExceptionHandler> logger)
        {
            _slackExceptionNotification = slackExceptionNotification;
            _logger = logger;
        }

        public void OnPerforming(PerformingContext filterContext)
        {
            // Log the start of the job execution
            var jobId = filterContext.BackgroundJob.Id;
            var jobName = filterContext.BackgroundJob.Job.Type.Name;
            _logger.LogInformation("Starting job {JobId} of type {JobName}", jobId, jobName);
        }

        public async void OnPerformed(PerformedContext filterContext)
        {
            // Log the completion of the job execution
            var exception = filterContext.Exception;
            var jobId = filterContext.BackgroundJob.Id;
            var jobName = filterContext.BackgroundJob.Job.Type.Name;
            _logger.LogInformation("Completed job {JobId} of type {JobName}", jobId, jobName);
            
            // Properly await the notification
            if(exception != null) {
                await _slackExceptionNotification.TryHandleBackgroundJobAsync(jobId, jobName, exception);
            }
        }

        public async void OnStateElection(ElectStateContext context)
        {
            if (context.CandidateState is FailedState failedState)
            {
                var exception = failedState.Exception;
                var jobId = context.BackgroundJob.Id;
                var jobName = context.BackgroundJob.Job.Type.Name;

                _logger.LogError(exception, "Job {JobId} of type {JobName} failed", jobId, jobName);
                
                // Properly await the notification
                await _slackExceptionNotification.TryHandleBackgroundJobAsync(jobId, jobName, exception);
            }
        }
    }
} 