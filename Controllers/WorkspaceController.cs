using Microsoft.AspNetCore.Mvc;
using QuireClone.Services;

namespace QuireClone.Controllers;

public sealed class WorkspaceController(WorkspaceService workspace) : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();

    [HttpGet("/api/workspace")]
    public async Task<IActionResult> Get([FromQuery] Guid? viewId, CancellationToken cancellationToken) =>
        Ok(await workspace.GetAsync(viewId, cancellationToken));

    [HttpGet("/error")]
    public IActionResult Error() => Problem("Ein unerwarteter Fehler ist aufgetreten.");
}
