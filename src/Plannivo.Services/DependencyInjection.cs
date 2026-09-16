using Plannivo.Repositories;
using Plannivo.Services.Implementations;
using Plannivo.Services.Implementations.Authentication;
using Plannivo.Services.Interfaces;
using Plannivo.Services.Interfaces.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Plannivo.Services;

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
