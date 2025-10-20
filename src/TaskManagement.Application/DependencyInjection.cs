using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Behaviors;

namespace TaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // MediatR - Automatically discovers and registers all IRequest/IRequestHandler implementations
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            
            // Add validation pipeline behavior
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        
        // FluentValidation - Automatically discovers and registers all validators
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        
        return services;
    }
}
