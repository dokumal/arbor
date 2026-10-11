using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/views")]
public sealed class ViewConfigurationsController(ViewConfigurationService views) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        var id = await views.CreateAsync(request, cancellationToken);
        return Created($"/api/views/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        await views.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await views.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
