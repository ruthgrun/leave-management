using LeaveManagement.Api.Data;
using LeaveManagement.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL by default — `docker compose up` brings one up next to the API.
// No Docker on the machine? Set Database:Provider=Sqlite and the app runs
// against a local file instead. See the README.
var useSqlite = string.Equals(
    builder.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<LeaveDbContext>(options =>
{
    if (useSqlite)
        options.UseSqlite(builder.Configuration.GetConnectionString("LeaveSqlite"));
    else
        options.UseNpgsql(builder.Configuration.GetConnectionString("Leave"));
});

builder.Services.AddScoped<LeaveRequestService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaveDbContext>();
    db.Database.EnsureCreated();
    LeaveDbContext.Seed(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAngular");
app.MapControllers();

app.Run();

// Exposed so the integration tests can reference the entry point.
public partial class Program { }
