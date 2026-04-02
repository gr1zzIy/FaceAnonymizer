using System.Net.Mime;

namespace FaceAnonymizer.Api.Middleware;

public sealed class ProblemDetailsExceptionMiddleware : IMiddleware
{
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    public ProblemDetailsExceptionMiddleware(ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Некоректний запит");
            await WriteProblem(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Некоректний запит");
            await WriteProblem(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Некоректна операція");
            await WriteProblem(context, StatusCodes.Status500InternalServerError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необроблений виняток");
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "Внутрішня помилка сервера");
        }
    }

    private static async Task WriteProblem(HttpContext ctx, int status, string detail)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = MediaTypeNames.Application.ProblemJson;
        await ctx.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = status >= 500 ? "Помилка сервера" : "Некоректний запит",
            status,
            detail
        });
    }
}
