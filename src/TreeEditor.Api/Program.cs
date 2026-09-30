using Microsoft.EntityFrameworkCore;
using TreeEditor.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
// the retrying execution strategy does not allow our own transactions in TreeService and SampleData
builder.AddNpgsqlDbContext<TreeDbContext>("treedb", settings => settings.DisableRetry = true);
builder.Services.AddScoped<TreeService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TreeDbContext>();
    await db.Database.MigrateAsync();
    if (!await db.Nodes.AnyAsync())
    {
        await SampleData.Reset(db);
    }
}

app.UseExceptionHandler();
app.MapNodeEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
