using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

public sealed class TimeEntriesController(TimeEntryService entries) : Controller
{
    [HttpGet("/aufwaende")]
    public IActionResult Index() => View();

    [HttpGet("/api/time-entries")]
    public async Task<IActionResult> Get([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await entries.GetAsync(from, to, cancellationToken));

    [HttpPost("/api/time-entries")]
    public async Task<IActionResult> Create([FromBody] TimeEntrySaveRequest request, CancellationToken cancellationToken)
    {
        var entry = await entries.CreateAsync(request, cancellationToken);
        return Created($"/api/time-entries/{entry.Id}", entry);
    }

    [HttpPut("/api/time-entries/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TimeEntrySaveRequest request, CancellationToken cancellationToken) =>
        Ok(await entries.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("/api/time-entries/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await entries.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
