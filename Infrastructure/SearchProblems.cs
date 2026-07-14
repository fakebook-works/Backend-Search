using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BackEndSearchFakebook.Infrastructure;

public static class SearchProblems
{
    public static ProblemDetails Create(
        HttpContext context,
        int statusCode,
        string title,
        string code)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }

    public static IActionResult Result(
        ControllerBase controller,
        int statusCode,
        string title,
        string code)
    {
        var result = controller.StatusCode(
            statusCode,
            Create(controller.HttpContext, statusCode, title, code));
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    public static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.ValidationState == ModelValidationState.Invalid)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The supplied value is invalid."
                        : error.ErrorMessage)
                    .ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request payload is invalid.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = "INVALID_INPUT";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        var result = new BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
