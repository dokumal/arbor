using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api")]
public sealed class CustomFieldsController(CustomFieldService fields) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/fields")]
    public async Task<IActionResult> Create(Guid projectId, CustomFieldSaveRequest request, CancellationToken cancellationToken)
    {
        var id = await fields.CreateAsync(projectId, request, cancellationToken);
        return Created($"/api/fields/{id}", new { id });
    }

    [HttpPut("fields/{id:guid}/options")]
    public async Task<IActionResult> UpdateOptions(Guid id, CustomFieldOptionsRequest request, CancellationToken cancellationToken)
    {
        await fields.UpdateOptionsAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("fields/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await fields.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
