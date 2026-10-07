using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

public sealed class SettingsController(StorageLocationService storage) : Controller
{
    [HttpGet("/settings")]
    public IActionResult Index() => View();

    [HttpGet("/api/settings")]
    public IActionResult Get() => Ok(storage.GetInfo());

    [HttpPut("/api/settings")]
    public async Task<IActionResult> Update([FromBody] StorageSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var message = await storage.ChangeAsync(new StoragePaths(request.DatabasePath, request.ImagesDirectory), cancellationToken);
            return Ok(new { message, settings = storage.GetInfo() });
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = $"Der Speicherort konnte nicht geändert werden: {exception.Message}" });
        }
    }
}
