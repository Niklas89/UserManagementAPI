using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace UserManagementAPI.Middleware;

public sealed class TokenAuthenticationMiddleware(RequestDelegate next, TokenValidator validator, RequestAudit audit)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var values = context.Request.Headers.Authorization;
        if (values.Count != 1 || !AuthenticationHeaderValue.TryParse(values[0], out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(header.Parameter) || !validator.IsValid(header.Parameter))
        {
            // Authentication short-circuits the later logging middleware.
            audit.Start(context);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            var problem = new ProblemDetails { Status = 401, Title = "Unauthorized", Detail = "A valid bearer token is required." };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: context.RequestAborted);
            return;
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "internal-service") }, "Bearer"));
        await next(context);
    }
}
