using Gatherly.Repositories;
using Gatherly.Services.Implementations;
using Gatherly.Services.Implementations.Authentication;
using Gatherly.Services.Interfaces;
using Gatherly.Services.Interfaces.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gatherly.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRepositories(configuration);

        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<ITokenService, TokenService>();

        return services;
    }
}
