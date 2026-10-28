using GameHub.Auth;
using GameHub.Games;
using GameHub.Shared.Modules;
using GameHub.Steam;
using Microsoft.AspNetCore.Diagnostics;

// The host only wires modules together. Add a feature by adding a module here.
IReadOnlyCollection<IModule> modules = [new AuthModule(), new GamesModule(), new SteamModule()];

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddModules(builder.Configuration, modules);

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    logger.LogError(exception, "Unhandled exception");

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new()
    {
        HttpContext = context,
        ProblemDetails = { Title = "An unexpected error occurred." },
    });
}));

// Angular is built into wwwroot and served by this same app, so there is no CORS to configure.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapModules(modules);

// Unknown /api/* routes stay 404 instead of returning the SPA.
app.MapFallback("/api/{**rest}", () => Results.NotFound());

// Client-side routes (e.g. /login) fall back to index.html. "nonfile" excludes paths with
// extensions (.js, .css), otherwise the endpoint would match them and static files would be skipped.
app.MapFallbackToFile("{*path:nonfile}", "index.html");

await app.Services.InitializeModulesAsync(modules);

app.Run();

public partial class Program;
