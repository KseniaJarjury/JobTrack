using Microsoft.AspNetCore.Antiforgery;

namespace JobTrack.API.Api;

public static class AntiforgeryEndpointExtensions
{
    public static RouteGroupBuilder ValidateAntiforgery(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(ValidateRequest);
        return group;
    }

    public static RouteHandlerBuilder ValidateAntiforgery(this RouteHandlerBuilder endpoint)
    {
        endpoint.AddEndpointFilter(ValidateRequest);
        return endpoint;
    }

    private static async ValueTask<object?> ValidateRequest(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            return await next(context);

        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new { message = "La solicitud de seguridad expiró. Actualizá la página e intentá nuevamente." });
        }

        return await next(context);
    }
}
