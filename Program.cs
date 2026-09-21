using UserManagementAPI.Services;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<UserStore>();
var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();
app.Run();
