using Microsoft.AspNetCore.Mvc;
using QuireClone.Services;

namespace QuireClone.Controllers;

public sealed class WorkspaceController(WorkspaceService workspace, ProjectService projects) : Controller
{
    // Remembers the last opened project, so the start page can open it again.
    private const string ProjectCookie = "arbor-project";

    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var remembered = Guid.TryParse(Request.Cookies[ProjectCookie], out var id) ? id : (Guid?)null;
        return Redirect($"/projekte/{await projects.StartProjectIdAsync(remembered, cancellationToken)}");
    }

    [HttpGet("/projekte/{projectId:guid}")]
    public async Task<IActionResult> Project(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            Response.Cookies.Delete(ProjectCookie);
            return Redirect("/");
        }
        Response.Cookies.Append(ProjectCookie, project.Id.ToString(), new CookieOptions
            { HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true, MaxAge = TimeSpan.FromDays(365) });
        ViewData["Project"] = project;
        return View("Index");
    }

    [HttpGet("/api/projects/{projectId:guid}/workspace")]
    public async Task<IActionResult> Get(Guid projectId, [FromQuery] Guid? viewId, CancellationToken cancellationToken) =>
        Ok(await workspace.GetAsync(projectId, viewId, cancellationToken));

    [HttpGet("/error")]
    public IActionResult Error() => Problem("Ein unerwarteter Fehler ist aufgetreten.");
}
