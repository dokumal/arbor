using Microsoft.AspNetCore.Mvc;
using QuireClone.Domain;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/task-images")]
public sealed class TaskImagesController(TaskImageStorage images) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType) throw new DomainValidationException("Ein Bild fehlt.");
        var files = (await Request.ReadFormAsync(cancellationToken)).Files;
        if (files.Count != 1) throw new DomainValidationException("Bitte genau ein Bild hochladen.");

        var (name, size) = await images.SaveAsync(files[0], cancellationToken);
        return Ok(new { url = $"/task-images/{name}", name, size });
    }

    [HttpGet("/task-images/{name}")]
    public IActionResult Get(string name)
    {
        var image = images.Find(name);
        return image is null ? NotFound() : PhysicalFile(image.Value.Path, image.Value.ContentType);
    }
}
