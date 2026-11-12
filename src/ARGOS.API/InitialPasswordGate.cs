namespace ARGOS.API;

/// <summary>Provisional sessions can only complete password setup or manage their session.</summary>
public sealed class InitialPasswordGate(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim("must_change_password", "true")
            && !IsSessionSetup(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { code = "Password.ChangeRequired" });
            return;
        }
        await next(context);
    }

    private static bool IsSessionSetup(HttpRequest request)
    {
        var path = request.Path.Value?.TrimEnd('/');
        if (HttpMethods.IsGet(request.Method))
            return new[] { "/api/session/status", "/login", "/login/index.html", "/login/app.js" }
                .Contains(path, StringComparer.OrdinalIgnoreCase);
        return HttpMethods.IsPost(request.Method) && new[] {
            "/api/session/password/change", "/api/session/logout", "/api/session/activity",
            "/api/session/begin", "/api/session/verify", "/api/session/cancel"
        }.Contains(path, StringComparer.OrdinalIgnoreCase);
    }
}
