using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using System.Diagnostics;
using InstrumentationInterface;

namespace Instrumentation
{
    /// <summary>
    /// Middleware que captura automáticamente métricas de todos los endpoints:
    /// - Cantidad de invocaciones por endpoint
    /// - Latencia/duración de cada request
    /// - Tasa de errores (status codes 4xx y 5xx)
    /// </summary>
    public class MetricsMiddleware
    {
        private const string DefaultLatencyFile = "/tmp/pharmago-chaos-latency-ms";
        private const string DefaultIncludeHealthFile = "/tmp/pharmago-chaos-include-health";

        private readonly RequestDelegate _next;
        private readonly ICustomMetrics _metrics;

        public MetricsMiddleware(RequestDelegate next, ICustomMetrics metrics)
        {
            _next = next;
            _metrics = metrics;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = context.Request.Path.Value ?? "/";
            var method = context.Request.Method;

            try
            {
                var latencyMs = GetConfiguredLatencyMs();
                if (latencyMs > 0 && ShouldDelayEndpoint(endpoint))
                {
                    await Task.Delay(latencyMs, context.RequestAborted);
                }

                await _next(context);
                stopwatch.Stop();

                // Registrar la petición exitosa
                var statusCode = context.Response.StatusCode;
                _metrics.RecordHttpRequest(endpoint, method, statusCode);
                _metrics.RecordEndpointDuration(endpoint, method, stopwatch.Elapsed.TotalMilliseconds);

                // Registrar como error si el status code es 4xx o 5xx
                if (statusCode >= 400)
                {
                    var errorType = statusCode >= 500 ? "server_error" : "client_error";
                    _metrics.RecordError(endpoint, errorType);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Registrar la excepción
                _metrics.RecordHttpRequest(endpoint, method, 500);
                _metrics.RecordEndpointDuration(endpoint, method, stopwatch.Elapsed.TotalMilliseconds);
                _metrics.RecordError(endpoint, ex.GetType().Name);

                // Re-lanzar la excepción para que sea manejada por otros middlewares
                throw;
            }
        }

        private static int GetConfiguredLatencyMs()
        {
            var path = Environment.GetEnvironmentVariable("PHARMAGO_CHAOS_LATENCY_FILE") ?? DefaultLatencyFile;
            if (!File.Exists(path))
            {
                return 0;
            }

            try
            {
                var rawValue = File.ReadAllText(path).Trim();
                return int.TryParse(rawValue, out var latencyMs) && latencyMs > 0 ? latencyMs : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static bool ShouldDelayEndpoint(string endpoint)
        {
            if (endpoint.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!endpoint.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var path = Environment.GetEnvironmentVariable("PHARMAGO_CHAOS_INCLUDE_HEALTH_FILE") ?? DefaultIncludeHealthFile;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var rawValue = File.ReadAllText(path).Trim();
                return rawValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || rawValue.Equals("1", StringComparison.OrdinalIgnoreCase)
                    || rawValue.Equals("yes", StringComparison.OrdinalIgnoreCase)
                    || rawValue.Equals("si", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Extension method para registrar el middleware fácilmente
    /// </summary>
    public static class MetricsMiddlewareExtensions
    {
        public static IApplicationBuilder UseMetricsMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MetricsMiddleware>();
        }
    }
}

