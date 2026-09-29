using Microsoft.AspNetCore.Mvc;
using NetCalendy.Domain.Common;

namespace NetCalendy.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess
            ? new OkResult()
            : result.Error.ToActionResult();

    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? new OkObjectResult(result.Value)
            : result.Error.ToActionResult();

    public static IActionResult ToCreatedResult<T>(this Result<T> result, string routeName, object routeValues) =>
        result.IsSuccess
            ? new CreatedAtRouteResult(routeName, routeValues, result.Value)
            : result.Error.ToActionResult();

    private static IActionResult ToActionResult(this Error error)
    {
        var status = error.Code switch
        {
            _ when error.Code.EndsWith(".NotFound") => 404,
            _ when error.Code.Contains(".Conflict") || error.Code.Contains("Taken") || error.Code.Contains("AlreadyCancelled") => 409,
            _ when error.Code.Contains("Forbidden") => 403,
            _ => 400,
        };

        return new ObjectResult(new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Description,
            Status = status,
        })
        { StatusCode = status };
    }
}
