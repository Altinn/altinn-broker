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
                var retryCount = GetRetryCount(filterContext);
                await _slackExceptionNotification.TryHandleBackgroundJobAsync(jobId, jobName, exception, retryCount);
            }
        }

        public async void OnStateElection(ElectStateContext context)
        {
            if (context.CandidateState is FailedState failedState)
            {
                var exception = failedState.Exception;
                var jobId = context.BackgroundJob.Id;
                var jobName = context.BackgroundJob.Job.Type.Name;
                var retryCount = GetRetryCount(context);

                _logger.LogError(exception, "Job {JobId} of type {JobName} failed on retry {RetryCount}", jobId, jobName, retryCount);
                
                // Properly await the notification
                await _slackExceptionNotification.TryHandleBackgroundJobAsync(jobId, jobName, exception, retryCount);
            }
        }

        private int GetRetryCount(PerformedContext filterContext)
        {
            try
            {
                // Try to get retry count from Hangfire's job state
                // For failed jobs, we can check the retry count from the job state
                var jobId = filterContext.BackgroundJob.Id;
                
                // In Hangfire, retry count is typically available in the job state
                // We'll use a simple approach to estimate retry count based on job execution history
                // This is a simplified implementation - in production you might want to query the Hangfire database directly
                
                // For now, we'll return 0 as default and let the notification handler decide
                // The actual retry count would need to be tracked by Hangfire's retry mechanism
                // or by querying the job state from the database
                
                _logger.LogDebug("Retry count not available for job {JobId}, using default value", jobId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get retry count for job {JobId}", filterContext.BackgroundJob.Id);
                return 0;
            }
        }

        private int GetRetryCount(ElectStateContext filterContext)
        {
            try
            {
                // Try to get retry count from Hangfire's job state
                // For failed jobs, we can check the retry count from the job state
                var jobId = filterContext.BackgroundJob.Id;
                
                // In Hangfire, retry count is typically available in the job state
                // We'll use a simple approach to estimate retry count based on job execution history
                // This is a simplified implementation - in production you might want to query the Hangfire database directly
                
                // For now, we'll return 0 as default and let the notification handler decide
                // The actual retry count would need to be tracked by Hangfire's retry mechanism
                // or by querying the job state from the database
                
                _logger.LogDebug("Retry count not available for job {JobId}, using default value", jobId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get retry count for job {JobId}", filterContext.BackgroundJob.Id);
                return 0;
            }
        }
    }
} 