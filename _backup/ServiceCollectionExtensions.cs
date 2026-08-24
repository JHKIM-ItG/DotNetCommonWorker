using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommonServiceProject;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommonWorker<TWorker>(this IServiceCollection services) 
        where TWorker : BaseWorkerService
    {
        services.AddSingleton<TWorker>();
        services.AddSingleton<BaseWorkerService>(sp => sp.GetRequiredService<TWorker>());
        services.AddHostedService<IHostedService>(sp => sp.GetRequiredService<TWorker>());
        return services;
    }
}
