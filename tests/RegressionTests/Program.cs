using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UserManagementAPI.Middleware;
using UserManagementAPI.Models;
using UserManagementAPI.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS {message}");
}
static UserRequest Input(string email) => new() { FirstName = "Ada", LastName = "Lovelace", Email = email };

var store = new UserStore();
var first = store.Create(Input("ada@example.com"))!;
var second = store.Create(Input("other@example.com"))!;
Check(store.Update(first.Id, Input(second.Email)) == UpdateResult.DuplicateEmail, "Conflicting update rejected");
Check(store.Create(Input(first.Email)) is null, "Failed update retains original email reservation");
Check(store.Update(first.Id, Input(" ADA@example.com ")) == UpdateResult.Updated, "Own email permits case and whitespace changes");
Check(store.Update(first.Id, Input("new@example.com")) == UpdateResult.Updated, "Email can change");
Check(store.Create(Input("ada@example.com")) is not null, "Old email released after update");
Check(store.Delete(first.Id) && store.Create(Input("new@example.com")) is not null, "Email released after deletion");
Check(store.Get(0) is null && !store.Delete(0) && store.Update(0, Input("a@b.com")) == UpdateResult.NotFound, "Missing users handled");

var concurrent = new UserStore();
Parallel.For(0, 100, i => concurrent.Create(Input(i % 2 == 0 ? "same@example.com" : "SAME@example.com")));
Check(concurrent.GetAll().Length == 1, "Concurrent duplicate creation has one winner");
Parallel.For(0, 100, i =>
{
    concurrent.Create(Input($"user{i}@example.com"));
    var snapshot = concurrent.GetAll();
    if (!snapshot.Select(user => user.Id).SequenceEqual(snapshot.Select(user => user.Id).Order()))
        throw new Exception("Unsorted snapshot");
});
Check(concurrent.GetAll().Length == 101, "Concurrent list and create preserve users and sorted snapshots");

var logger = NullLogger<ApiExceptionMiddleware>.Instance;
var audit = new RequestAudit(NullLogger<RequestLoggingMiddleware>.Instance);
var context = new DefaultHttpContext();
context.TraceIdentifier = "test-trace";
context.Response.Body = new MemoryStream();
var calls = 0;
var middleware = new ApiExceptionMiddleware(async ctx =>
{
    if (++calls == 1) throw new InvalidOperationException("SECRET exception detail");
    await ctx.Response.WriteAsync("healthy");
}, logger, audit);
await middleware.InvokeAsync(context);
context.Response.Body.Position = 0;
var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
using var json = JsonDocument.Parse(body);
Check(context.Response.StatusCode == 500 && context.Response.ContentType!.StartsWith("application/problem+json"), "Unexpected exception returns problem JSON with 500");
Check(json.RootElement.GetProperty("traceId").GetString() == "test-trace" && !body.Contains("SECRET"), "Error includes trace ID without exception details");
var nextContext = new DefaultHttpContext();
nextContext.Response.Body = new MemoryStream();
await middleware.InvokeAsync(nextContext);
Check(nextContext.Response.StatusCode == 200 && nextContext.Response.Body.Length > 0, "Requests continue after a handled exception");
var canceledContext = new DefaultHttpContext();
canceledContext.RequestAborted = new CancellationToken(true);
await new ApiExceptionMiddleware(_ => throw new OperationCanceledException(), logger, audit).InvokeAsync(canceledContext);
Check(canceledContext.Response.StatusCode != 500, "Client cancellation is not reported as a server failure");
Console.WriteLine("All regression checks passed.");
await MiddlewareChecks.Run();
