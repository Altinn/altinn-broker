using System.Collections.Concurrent;
using System.Net;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.Filters;

/// <summary>
/// Authorization filter that validates API key for report generation endpoints
/// Implements rate limiting per IP address to prevent abuse
/// </summary>
public class ReportApiKeyFilter : IAuthorizationFilter
{
    private readonly ILogger<ReportApiKeyFilter> _logger;
    private readonly string _validApiKey;
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _requestLog = new();
    private const int RateLimitMaxRequests = 10; // Maximum 10 requests
    private const int RateLimitWindowMinutes = 1; // Within 1 minute

    public ReportApiKeyFilter(ILogger<ReportApiKeyFilter> logger, IOptions<ReportApiKeyOptions> options)
    {
        _logger = logger;
        _validApiKey = options.Value.ApiKey ?? throw new InvalidOperationException("Report API key is not configured");
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var clientIp = GetClientIpAddress(context.HttpContext);
        var rateLimitInfo = GetRateLimitInfo(clientIp);

        // Add rate limit headers to response
        AddRateLimitHeaders(context.HttpContext, rateLimitInfo);

        // Check rate limiting first
        if (rateLimitInfo.Remaining <= 0)
        {
            _logger.LogWarning("Rate limit exceeded for IP: {ClientIp}", clientIp);
            context.Result = new ContentResult
            {
                StatusCode = (int)HttpStatusCode.TooManyRequests,
                Content = "Rate limit exceeded. Maximum 10 requests per minute allowed."
            };
            return;
        }

        // Check API key
        if (!context.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
        {
            _logger.LogWarning("Missing API key in request from IP: {ClientIp}", clientIp);
            context.Result = new ContentResult
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Content = "Missing API key. Please provide X-API-Key header."
            };
            return;
        }

        if (!string.Equals(extractedApiKey, _validApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid API key attempt from IP: {ClientIp}", clientIp);
            context.Result = new ContentResult
            {
                StatusCode = (int)HttpStatusCode.Forbidden,
                Content = "Invalid API key."
            };
            return;
        }

        // Log this successful request for rate limiting
        LogRequest(clientIp);

        _logger.LogInformation("Successful API key authentication from IP: {ClientIp}", clientIp);
    }

    private static RateLimitInfo GetRateLimitInfo(string clientIp)
    {
        var now = DateTime.UtcNow;
        var requests = _requestLog.GetOrAdd(clientIp, _ => new Queue<DateTime>());

        lock (requests)
        {
            // Remove requests older than the rate limit window
            while (requests.Count > 0 && requests.Peek() < now.AddMinutes(-RateLimitWindowMinutes))
            {
                requests.Dequeue();
            }

            var remaining = Math.Max(0, RateLimitMaxRequests - requests.Count);
            var resetTime = requests.Count > 0 
                ? requests.Peek().AddMinutes(RateLimitWindowMinutes)
                : now.AddMinutes(RateLimitWindowMinutes);

            return new RateLimitInfo
            {
                Limit = RateLimitMaxRequests,
                Remaining = remaining,
                Reset = resetTime
            };
        }
    }

    private static void LogRequest(string clientIp)
    {
        var now = DateTime.UtcNow;
        var requests = _requestLog.GetOrAdd(clientIp, _ => new Queue<DateTime>());

        lock (requests)
        {
            requests.Enqueue(now);
        }
    }

    private static void AddRateLimitHeaders(HttpContext context, RateLimitInfo rateLimitInfo)
    {
        context.Response.Headers["X-RateLimit-Limit"] = rateLimitInfo.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = rateLimitInfo.Remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(rateLimitInfo.Reset).ToUnixTimeSeconds().ToString();
        
        if (rateLimitInfo.Remaining <= 0)
        {
            var retryAfter = (int)Math.Ceiling((rateLimitInfo.Reset - DateTime.UtcNow).TotalSeconds);
            context.Response.Headers["Retry-After"] = retryAfter.ToString();
        }
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Try to get IP from X-Forwarded-For header (in case of proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // Fallback to RemoteIpAddress
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Configuration options for Report API key authentication
/// </summary>
public class ReportApiKeyOptions
{
    public const string SectionName = "ReportApiKey";
    
    /// <summary>
    /// The API key used for authenticating report generation requests
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Rate limit information for a client
/// </summary>
internal class RateLimitInfo
{
    public int Limit { get; init; }
    public int Remaining { get; init; }
    public DateTime Reset { get; init; }
}

