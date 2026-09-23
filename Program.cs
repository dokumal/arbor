using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuireClone.Data;
using QuireClone.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Postgres");
LocalPostgres? localPostgres = null;
if (string.IsNullOrWhiteSpace(connectionString))
{
    localPostgres = await LocalPostgres.StartAsync(builder.Environment.ContentRootPath);
    connectionString = localPostgres.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<TaskService>();
builder.Services.AddSingleton<TaskImageStorage>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Workspace}/{action=Index}/{id?}");
try
{
    await DatabaseInitializer.InitializeAsync(app.Services);
    await app.RunAsync();
}
finally
{
    if (localPostgres is not null) await localPostgres.StopAsync();
}
