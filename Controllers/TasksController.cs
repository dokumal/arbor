using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(TaskSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var task = await tasks.SaveAsync(null, request, cancellationToken);
            return Created($"/api/tasks/{task.Id}", new { task.Id });
        }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, TaskSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await tasks.SaveAsync(id, request, cancellationToken);
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
            await tasks.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, TaskMoveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await tasks.MoveAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

}
