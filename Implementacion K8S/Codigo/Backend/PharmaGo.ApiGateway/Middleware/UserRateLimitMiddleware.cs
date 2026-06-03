using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace PharmaGo.ApiGateway.Middleware
{
    /// <summary>
    /// Rate limiting basado en el token de autenticación del usuario
    /// en lugar de la IP, para evitar bloquear múltiples usuarios detrás de la misma IP
    /// </summary>
    public class UserRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UserRateLimitMiddleware> _logger;
        
        // Configuración
        private readonly int _maxRequestsPerMinute;
        private readonly int _maxRequestsPerHour;

        public UserRateLimitMiddleware(
            RequestDelegate next, 
            IMemoryCache cache,
            ILogger<UserRateLimitMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
            _maxRequestsPerMinute = configuration.GetValue("RateLimiting:MaxRequestsPerMinute", 100);
            _maxRequestsPerHour = configuration.GetValue("RateLimiting:MaxRequestsPerHour", 1000);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Obtener identificador del usuario (token o IP como fallback)
            var identifier = GetUserIdentifier(context);
            
            // Verificar límites
            if (!CheckRateLimit(identifier, out string reason))
            {
                _logger.LogWarning(
                    "Rate limit exceeded for {Identifier}: {Reason}",
                    identifier,
                    reason
                );
                                
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers.Add("X-RateLimit-Reason", reason);
                await context.Response.WriteAsJsonAsync(new 
                { 
                    error = "Too many requests",
                    message = reason,
                    retryAfter = "60 seconds"
                });
                return;
            }

            await _next(context);
        }

        private string GetUserIdentifier(HttpContext context)
        {
            // 1. Intentar obtener token de autorización
            if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var token = authHeader.ToString().Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token))
                {
                    return $"user:{token.Substring(0, Math.Min(8, token.Length))}"; // Usar primeros 8 chars
                }
            }

            // 2. Intentar obtener X-User-Id (si el frontend lo envía)
            if (context.Request.Headers.TryGetValue("X-User-Id", out var userId))
            {
                return $"user:{userId}";
            }

            // 3. Fallback a IP (para endpoints públicos como login)
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            // Si viene de un proxy, intentar obtener la IP real
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                ip = forwardedFor.ToString().Split(',')[0].Trim();
            }
            else if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                ip = realIp.ToString();
            }

            return $"ip:{ip}";
        }

        private bool CheckRateLimit(string identifier, out string reason)
        {
            var now = DateTime.UtcNow;
            
            // Verificar límite por minuto
            var minuteKey = $"{identifier}:minute:{now:yyyyMMddHHmm}";
            var minuteCount = _cache.GetOrCreate(minuteKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return 0;
            });

            if (minuteCount >= _maxRequestsPerMinute)
            {
                reason = $"Exceeded {_maxRequestsPerMinute} requests per minute";
                return false;
            }

            // Verificar límite por hora
            var hourKey = $"{identifier}:hour:{now:yyyyMMddHH}";
            var hourCount = _cache.GetOrCreate(hourKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return 0;
            });

            if (hourCount >= _maxRequestsPerHour)
            {
                reason = $"Exceeded {_maxRequestsPerHour} requests per hour";
                return false;
            }

            // Incrementar contadores
            _cache.Set(minuteKey, minuteCount + 1, TimeSpan.FromMinutes(1));
            _cache.Set(hourKey, hourCount + 1, TimeSpan.FromHours(1));

            reason = string.Empty;
            return true;
        }
    }

    public static class UserRateLimitMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserRateLimit(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserRateLimitMiddleware>();
        }
    }
}

