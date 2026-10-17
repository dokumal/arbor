using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api")]
public sealed class WorkspaceItemsController(WorkspaceItemService items) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/items")]
    public async Task<IActionResult> Create(Guid projectId, ItemSaveRequest request, CancellationToken cancellationToken)
    {
        var item = await items.CreateAsync(projectId, request, cancellationToken);
        return Created($"/api/items/{item.Id}", new { item.Id });
    }

    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ItemSaveRequest request, CancellationToken cancellationToken)
    {
        await items.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await items.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("items/{id:guid}/dates")]
    public async Task<IActionResult> UpdateDates(Guid id, TaskDatesRequest request, CancellationToken cancellationToken)
    {
        await items.UpdateDatesAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("items/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, ItemStatusRequest request, CancellationToken cancellationToken)
    {
        await items.UpdateStatusAsync(id, request.StatusId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("items/{id:guid}/fields/{fieldId:guid}")]
    public async Task<IActionResult> UpdateField(Guid id, Guid fieldId, ItemFieldValueRequest request, CancellationToken cancellationToken)
    {
        await items.UpdateFieldAsync(id, fieldId, request.Value, cancellationToken);
        return NoContent();
    }

    [HttpPost("items/{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, TaskMoveRequest request, CancellationToken cancellationToken)
    {
        await items.MoveAsync(id, request, cancellationToken);
        return NoContent();
    }
}
