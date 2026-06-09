using Microsoft.Extensions.DependencyInjection;

namespace TFoodies.Application;

/// <summary>
/// Composition root for the Application layer. Will register MediatR handlers,
/// FluentValidation validators, and pipeline behaviors (Validation/Transaction/Logging)
/// as those land. Kept as a single entry point so the Functions host stays thin.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // TODO: services.AddMediatR(...); validators; pipeline behaviors.
        return services;
    }
}
