using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommonServiceProject;

/// <summary>
/// BaseWorkerService 파생 워커를 DI 컨테이너에 등록하는 확장 메서드.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// TWorker를 Singleton으로 등록하고 BaseWorkerService, IHostedService로도 바인딩합니다.
    /// 여러 번 호출하여 다수의 워커를 동시에 등록할 수 있습니다.
    /// </summary>
    public static IServiceCollection AddCommonWorker<TWorker>(this IServiceCollection services)
        where TWorker : BaseWorkerService
    {
        services.AddSingleton<TWorker>();
        services.AddSingleton<BaseWorkerService>(sp => sp.GetRequiredService<TWorker>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TWorker>());

        return services;
    }
}

