using UserManagementAPI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<UserStore>();
var app = builder.Build();
app.UseExceptionHandler();
app.MapControllers();
app.Run();
