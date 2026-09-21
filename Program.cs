using UserManagementAPI.Services;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<TokenValidator>();
builder.Services.AddSingleton<RequestAudit>();
var app = builder.Build();
// Fail at startup rather than accidentally serving an unsecured API.
_ = app.Services.GetRequiredService<TokenValidator>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.MapControllers();
app.Run();
