using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/fields")]
public sealed class CustomFieldsController(CustomFieldService fields) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CustomFieldSaveRequest request, CancellationToken cancellationToken)
    {
        var id = await fields.CreateAsync(request, cancellationToken);
        return Created($"/api/fields/{id}", new { id });
    }

    [HttpPut("{id:guid}/options")]
    public async Task<IActionResult> UpdateOptions(Guid id, CustomFieldOptionsRequest request, CancellationToken cancellationToken)
    {
        await fields.UpdateOptionsAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await fields.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
