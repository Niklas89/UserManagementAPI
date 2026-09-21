using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserManagementAPI.Middleware;

internal static class MiddlewareChecks
{
    public static async Task Run()
    {
        static void Check(bool result, string message)
        {
            if (!result) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        foreach (var invalid in new[] { "", "short", new string(' ', 40) })
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Token"] = invalid }).Build();
            var rejected = false;
            try { _ = new TokenValidator(config); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Invalid token configuration rejected");
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var logs = new AuditLogs();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Token"] = token });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton<TokenValidator>();
        builder.Services.AddSingleton<RequestAudit>();
        await using var app = builder.Build();
        app.UseMiddleware<ApiExceptionMiddleware>();
        app.UseMiddleware<TokenAuthenticationMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.MapGet("/ok", (HttpContext ctx) => Results.Json(new { authenticated = ctx.User.Identity!.IsAuthenticated }));
        app.MapGet("/throw", (HttpContext _) => Task.FromException(new InvalidOperationException("PRIVATE_EXCEPTION_DETAIL")));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var requestCount = 0;
            async Task Send(string path, string? authorization, HttpStatusCode status)
            {
                var before = logs.Messages.Count;
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
                using var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                Check(response.StatusCode == status, $"HTTP {path} returns {(int)status}");
                if (status == HttpStatusCode.Unauthorized)
                {
                    Check(response.Headers.WwwAuthenticate.Any(x => x.Scheme == "Bearer"), "401 has bearer challenge");
                    Check(JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32() == 401, "401 is JSON problem details");
                }
                if (status == HttpStatusCode.InternalServerError)
                    Check(!body.Contains("PRIVATE_EXCEPTION_DETAIL") && JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32() == 500, "500 hides exception details");
                if (status == HttpStatusCode.OK)
                    Check(JsonDocument.Parse(body).RootElement.GetProperty("authenticated").GetBoolean(), "Valid token sets authenticated identity");
                // The client can receive headers just before the outer finally runs.
                for (var i = 0; i < 100 && logs.Messages.Count < before + 2; i++) await Task.Delay(10);
                var entries = logs.Messages.ToArray().Skip(before).ToArray();
                Check(entries.Length == 2 && entries[0].Contains("Incoming GET " + path) && entries[1].Contains("StatusCode=" + (int)status), "Audit logs incoming request and final response once");
                requestCount++;
            }
            foreach (var header in new string?[] { null, "Bearer invalid", "Basic abc", "Bearer", "Bearer a,b" })
                await Send("/ok", header, HttpStatusCode.Unauthorized);
            await Send("/ok", "Bearer " + token, HttpStatusCode.OK);
            await Send("/ok", "bearer " + token, HttpStatusCode.OK);
            await Send("/throw", null, HttpStatusCode.Unauthorized);
            await Send("/throw", "Bearer " + token, HttpStatusCode.InternalServerError);
            await Send("/ok", "Bearer " + token, HttpStatusCode.OK);
            await Send("/missing", "Bearer " + token, HttpStatusCode.NotFound);
            Check(!string.Join('\n', logs.Messages).Contains(token), "Audit logs never contain token");
            Console.WriteLine($"All middleware checks passed ({requestCount} HTTP requests).");
        }
        finally { await app.StopAsync(); }
    }

    private sealed class AuditLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, Messages);
        public void Dispose() { }
        private sealed class Capture(string category, ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category == typeof(RequestLoggingMiddleware).FullName) messages.Enqueue(formatter(state, exception));
            }
        }
    }
}
