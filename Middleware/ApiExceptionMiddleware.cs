using Microsoft.AspNetCore.Mvc;

namespace UserManagementAPI.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger, RequestAudit audit)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is no response to deliver.
            logger.LogDebug("Request {TraceId} was canceled by the client.", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            audit.Start(context);
            logger.LogError(exception, "Unhandled request error. TraceId: {TraceId}", context.TraceIdentifier);
            // A response already being streamed cannot safely be replaced with JSON.
            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "Please try again later."
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
        finally
        {
            // Capture the final status after error handling, including short-circuited 401s.
            audit.Complete(context);
        }
    }
}
