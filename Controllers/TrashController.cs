using Microsoft.AspNetCore.Mvc;
using QuireClone.Services;

namespace QuireClone.Controllers;

public sealed class TrashController(TrashService trash) : Controller
{
    [HttpGet("/papierkorb")]
    public IActionResult Index() => View();

    [HttpGet("/api/trash")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await trash.ListAsync(cancellationToken));

    [HttpPost("/api/trash/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken) =>
        Ok(await trash.RestoreAsync(id, cancellationToken));

    [HttpDelete("/api/trash/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await trash.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("/api/trash")]
    public async Task<IActionResult> EmptyTrash(CancellationToken cancellationToken)
    {
        await trash.EmptyAsync(cancellationToken);
        return NoContent();
    }
}
