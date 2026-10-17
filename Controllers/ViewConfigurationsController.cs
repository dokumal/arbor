using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api")]
public sealed class ViewConfigurationsController(ViewConfigurationService views) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/views")]
    public async Task<IActionResult> Create(Guid projectId, ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        var id = await views.CreateAsync(projectId, request, cancellationToken);
        return Created($"/api/views/{id}", new { id });
    }

    [HttpPut("views/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        await views.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("views/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await views.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
