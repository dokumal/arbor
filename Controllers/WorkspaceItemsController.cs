using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/items")]
public sealed class WorkspaceItemsController(WorkspaceItemService items) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(ItemSaveRequest request, CancellationToken cancellationToken)
    {
        var item = await items.SaveAsync(null, request, cancellationToken);
        return Created($"/api/items/{item.Id}", new { item.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ItemSaveRequest request, CancellationToken cancellationToken)
    {
        await items.SaveAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await items.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/dates")]
    public async Task<IActionResult> UpdateDates(Guid id, TaskDatesRequest request, CancellationToken cancellationToken)
    {
        await items.UpdateDatesAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/fields/{fieldId:guid}")]
    public async Task<IActionResult> UpdateField(Guid id, Guid fieldId, ItemFieldValueRequest request, CancellationToken cancellationToken)
    {
        await items.UpdateFieldAsync(id, fieldId, request.Value, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, TaskMoveRequest request, CancellationToken cancellationToken)
    {
        await items.MoveAsync(id, request, cancellationToken);
        return NoContent();
    }
}
