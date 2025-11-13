using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

namespace Altinn.Broker.API.Filters;

/// <summary>
/// Authorization filter to validate API key and enforce IP-based rate limiting for statistics endpoints
/// </summary>
public class StatisticsApiKeyFilter : IAsyncAuthorizationFilter
{
    private const int RateLimitWindowMinutes = 60;
    
    private readonly IConfiguration _configuration;
    private readonly ILogger<StatisticsApiKeyFilter> _logger;
    private readonly IDistributedCache _cache;
    private readonly IHostEnvironment _environment;

    public StatisticsApiKeyFilter(
        IConfiguration configuration, 
        ILogger<StatisticsApiKeyFilter> logger,
        IDistributedCache cache,
        IHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
        _environment = environment;
    }

    private int RateLimitAttempts => _environment.IsDevelopment() ? 5 : 10;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Only apply to statistics endpoints
        if (!context.HttpContext.Request.Path.StartsWithSegments("/broker/api/v1/statistics"))
        {
            return; // Not a statistics endpoint, let it pass
        }

        _logger.LogDebug("Statistics endpoint accessed, validating API key and rate limit");

        // Check if API key is provided
        if (!context.HttpContext.Request.Headers.TryGetValue("X-API-Key", out StringValues apiKeyHeader))
        {
            _logger.LogWarning("Statistics endpoint accessed without API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
            context.Result = new UnauthorizedObjectResult(new { error = "API key required for statistics endpoints" });
            return;
        }

        var providedApiKey = apiKeyHeader.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            _logger.LogWarning("Statistics endpoint accessed with empty API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
            context.Result = new UnauthorizedObjectResult(new { error = "API key cannot be empty" });
            return;
        }

        // Get configured API key
        var configuredApiKey = _configuration["StatisticsApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            _logger.LogError("StatisticsApiKey is not configured in application settings");
            context.Result = new UnauthorizedObjectResult(new { error = "API key validation not configured" });
            return;
        }

        // Validate API key using constant-time comparison
        if (!SecureEquals(providedApiKey, configuredApiKey))
        {
            _logger.LogWarning("Statistics endpoint accessed with invalid API key from IP: {ClientIp}", GetClientIpAddress(context.HttpContext));
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid API key" });
            return;
        }

        // Check rate limit using client IP as identifier
        var clientIp = GetClientIpAddress(context.HttpContext) ?? "unknown";
        var rateLimitResult = await CheckRateLimitAsync(clientIp);
        
        if (!rateLimitResult.IsAllowed)
        {
            _logger.LogWarning("Rate limit exceeded for IP: {ClientIp}. " +
                "Remaining attempts: {RemainingAttempts}, Reset time: {ResetTime}",
                clientIp, rateLimitResult.RemainingAttempts, rateLimitResult.ResetTime);

            context.Result = new ObjectResult(new { 
                error = "Rate limit exceeded", 
                retryAfter = rateLimitResult.RetryAfterSeconds,
                resetTime = rateLimitResult.ResetTime
            })
            {
                StatusCode = 429 // Too Many Requests
            };

             // Add rate limit headers
             context.HttpContext.Response.Headers["X-RateLimit-Limit"] = RateLimitAttempts.ToString();
             context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = rateLimitResult.RemainingAttempts.ToString();
             context.HttpContext.Response.Headers["X-RateLimit-Reset"] = rateLimitResult.ResetTime.ToUnixTimeSeconds().ToString();
             context.HttpContext.Response.Headers["Retry-After"] = rateLimitResult.RetryAfterSeconds.ToString();
            
            return;
        }

        // Add rate limit headers for successful requests
        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = RateLimitAttempts.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = rateLimitResult.RemainingAttempts.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Reset"] = rateLimitResult.ResetTime.ToUnixTimeSeconds().ToString();

        _logger.LogInformation("Statistics endpoint accessed with valid API key from IP: {ClientIp}. " +
            "Rate limit: {RemainingAttempts}/{Limit} remaining",
            clientIp, rateLimitResult.RemainingAttempts, RateLimitAttempts);
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP first (in case of proxy/load balancer)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            return forwardedFor.FirstOrDefault()?.Split(',')[0].Trim();
        }

        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            return realIp.FirstOrDefault();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks
    /// </summary>
    private static bool SecureEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Checks if the client has exceeded the rate limit
    /// </summary>
    /// <param name="clientIdentifier">Unique identifier for the client (IP address)</param>
    /// <returns>Rate limit check result</returns>
    private async Task<RateLimitResult> CheckRateLimitAsync(string clientIdentifier)
    {
        
        var cacheKey = $"rate_limit:statistics:{clientIdentifier}";
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-RateLimitWindowMinutes);

        try
        {
            // Get existing rate limit data
            var existingDataJson = await _cache.GetStringAsync(cacheKey);
            var rateLimitData = existingDataJson != null 
                ? JsonSerializer.Deserialize<RateLimitData>(existingDataJson) ?? new RateLimitData { Requests = new List<DateTimeOffset>() }
                : new RateLimitData { Requests = new List<DateTimeOffset>() };

            // Remove requests outside the current window
            rateLimitData.Requests = rateLimitData.Requests
                .Where(requestTime => requestTime > windowStart)
                .ToList();

            // Check if limit is exceeded BEFORE adding current request
            if (rateLimitData.Requests.Count >= RateLimitAttempts)
            {
                var oldestRequest = rateLimitData.Requests.Min();
                var resetTime = oldestRequest.AddMinutes(RateLimitWindowMinutes);
                
                _logger.LogWarning("Rate limit exceeded for IP {ClientIdentifier}. " +
                    "Attempts: {Attempts}/{Limit}, Reset at: {ResetTime}",
                    clientIdentifier, rateLimitData.Requests.Count, RateLimitAttempts, resetTime);

                return new RateLimitResult
                {
                    IsAllowed = false,
                    RemainingAttempts = 0,
                    ResetTime = resetTime,
                    RetryAfterSeconds = (int)(resetTime - now).TotalSeconds
                };
            }

            // Add current request
            rateLimitData.Requests.Add(now);

            // Store updated data atomically
            // Note: This is not fully atomic across distributed cache, but reduces race condition window
            var updatedDataJson = JsonSerializer.Serialize(rateLimitData);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = now.AddMinutes(RateLimitWindowMinutes + 1) // Add buffer
            };
            
            await _cache.SetStringAsync(cacheKey, updatedDataJson, cacheOptions);

            var remainingAttempts = RateLimitAttempts - rateLimitData.Requests.Count;
            var nextResetTime = rateLimitData.Requests.Min().AddMinutes(RateLimitWindowMinutes);

            _logger.LogDebug("Rate limit check for IP {ClientIdentifier}. " +
                "Attempts: {Attempts}/{Limit}, Remaining: {Remaining}",
                clientIdentifier, rateLimitData.Requests.Count, RateLimitAttempts, remainingAttempts);

            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingAttempts = remainingAttempts,
                ResetTime = nextResetTime,
                RetryAfterSeconds = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for IP {ClientIdentifier}", clientIdentifier);
            
            // In case of error, allow the request but log the issue
            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingAttempts = RateLimitAttempts,
                ResetTime = now.AddMinutes(RateLimitWindowMinutes),
                RetryAfterSeconds = 0
            };
        }
    }
}

/// <summary>
/// Result of a rate limit check
/// </summary>
public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public int RemainingAttempts { get; set; }
    public DateTimeOffset ResetTime { get; set; }
    public int RetryAfterSeconds { get; set; }
}

/// <summary>
/// Data structure for storing rate limit information in cache
/// </summary>
internal class RateLimitData
{
    public List<DateTimeOffset> Requests { get; set; } = new();
}

