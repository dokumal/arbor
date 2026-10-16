using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/statuses")]
public sealed class StatusesController(StatusService statuses) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> Save(TaskStatusesRequest request, CancellationToken cancellationToken)
    {
        await statuses.SaveAsync(request, cancellationToken);
        return NoContent();
    }
}
