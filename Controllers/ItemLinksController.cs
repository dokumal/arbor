using Microsoft.AspNetCore.Mvc;
using QuireClone.Contracts;
using QuireClone.Services;

namespace QuireClone.Controllers;

[ApiController]
[Route("api/items/{id:guid}/links")]
public sealed class ItemLinksController(ItemLinkService links) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid id, ItemLinkRequest request, CancellationToken cancellationToken)
    {
        try { await links.AddAsync(id, request.RelatedItemId, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpDelete("{relatedId:guid}")]
    public async Task<IActionResult> Delete(Guid id, Guid relatedId, CancellationToken cancellationToken)
    {
        await links.RemoveAsync(id, relatedId, cancellationToken);
        return NoContent();
    }
}
