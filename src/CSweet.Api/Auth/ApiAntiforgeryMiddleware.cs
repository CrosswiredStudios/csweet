using Microsoft.AspNetCore.Antiforgery;

namespace CSweet.Api.Auth;

public sealed class ApiAntiforgeryMiddleware
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace
    };

    private readonly RequestDelegate _next;

    public ApiAntiforgeryMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery, IWebHostEnvironment environment)
    {
        if (!environment.IsEnvironment("Testing") &&
            context.User.Identity?.IsAuthenticated == true &&
            context.Request.Path.StartsWithSegments("/api") &&
            !SafeMethods.Contains(context.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await Results.Problem(
                    title: "Invalid antiforgery token.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_antiforgery_token" })
                    .ExecuteAsync(context);
                return;
            }
        }

        await _next(context);
    }
}
