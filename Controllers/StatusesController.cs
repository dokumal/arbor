using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/statuses")]
public sealed class StatusesController(StatusService statuses) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> Save(Guid projectId, TaskStatusesRequest request, CancellationToken cancellationToken)
    {
        await statuses.SaveAsync(projectId, request, cancellationToken);
        return NoContent();
    }
}
