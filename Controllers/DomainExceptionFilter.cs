using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using QuireClone.Domain;

namespace QuireClone.Controllers;

/// <summary>
/// Turns rule violations from the services into ProblemDetails responses whose <c>detail</c> holds the message for the user.
/// Every other exception keeps the default handling.
/// </summary>
public sealed class DomainExceptionFilter(ProblemDetailsFactory problemDetails) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, title) = context.Exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Ungültige Anfrage"),
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Nicht gefunden"),
            _ => (0, "")
        };
        if (status == 0) return;
        var problem = problemDetails.CreateProblemDetails(context.HttpContext, status, title, detail: context.Exception.Message);
        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
