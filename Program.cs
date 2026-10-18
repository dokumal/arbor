using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuireClone.Controllers;
using QuireClone.Data;
using QuireClone.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(services =>
    new StorageSettings(services.GetRequiredService<IWebHostEnvironment>().ContentRootPath));
// Options are built per request, so a changed database location applies to the next request.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<StorageSettings>().ConnectionString));
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<WorkspaceItemService>();
builder.Services.AddScoped<ItemLinkService>();
builder.Services.AddScoped<TrashService>();
builder.Services.AddScoped<StatusService>();
builder.Services.AddScoped<CustomFieldService>();
builder.Services.AddScoped<ViewConfigurationService>();
builder.Services.AddScoped<TimeEntryService>();
builder.Services.AddSingleton<TaskImageStorage>();
builder.Services.AddSingleton<StorageLocationService>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<DomainExceptionFilter>();
})
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Scripts import each other as ES modules without a version in the URL, so browsers must revalidate them;
// otherwise a cached module can survive an update. Unchanged files still come back as a cheap 304.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.File.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            context.Context.Response.Headers.CacheControl = "no-cache";
    }
});
app.UseRouting();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Workspace}/{action=Index}/{id?}");
await DatabaseInitializer.InitializeAsync(app.Services);
await app.RunAsync();
