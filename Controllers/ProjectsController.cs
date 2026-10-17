using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(ProjectService projects) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await projects.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(ProjectSaveRequest request, CancellationToken cancellationToken)
    {
        var project = await projects.CreateAsync(request, cancellationToken);
        return Created($"/api/projects/{project.Id}", project);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, ProjectSaveRequest request, CancellationToken cancellationToken)
    {
        await projects.RenameAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await projects.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
