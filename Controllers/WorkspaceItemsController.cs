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
        try
        {
            var item = await items.SaveAsync(null, request, cancellationToken);
            return Created($"/api/items/{item.Id}", new { item.Id });
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ItemSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await items.SaveAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await items.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPatch("{id:guid}/dates")]
    public async Task<IActionResult> UpdateDates(Guid id, TaskDatesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await items.UpdateDatesAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, TaskMoveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await items.MoveAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

}
