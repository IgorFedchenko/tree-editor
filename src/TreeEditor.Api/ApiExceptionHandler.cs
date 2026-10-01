using Microsoft.AspNetCore.Diagnostics;

namespace TreeEditor.Api;

public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            NodeNotFoundException => StatusCodes.Status404NotFound,
            NodeDeletedException => StatusCodes.Status409Conflict,
            InvalidRequestException => StatusCodes.Status400BadRequest,
            BadHttpRequestException e => e.StatusCode,
            _ => 0,
        };
        if (status == 0)
        {
            return false;
        }

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = status, Detail = exception.Message },
        });
    }
}
