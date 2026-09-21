namespace UserManagementAPI.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, RequestAudit audit)
{
    public Task InvokeAsync(HttpContext context)
    {
        audit.Start(context);
        return next(context);
    }
}

public sealed class RequestAudit(ILogger<RequestLoggingMiddleware> logger)
{
    private static readonly object StartedKey = new();

    public void Start(HttpContext context)
    {
        if (context.Items.ContainsKey(StartedKey)) return;
        context.Items[StartedKey] = true;
        logger.LogInformation("Incoming {Method} {Path} TraceId={TraceId}",
            context.Request.Method, context.Request.Path.Value, context.TraceIdentifier);
    }

    public void Complete(HttpContext context)
    {
        Start(context);
        logger.LogInformation("Outgoing {Method} {Path} StatusCode={StatusCode} Aborted={Aborted} TraceId={TraceId}",
            context.Request.Method, context.Request.Path.Value, context.Response.StatusCode,
            context.RequestAborted.IsCancellationRequested, context.TraceIdentifier);
    }
}
