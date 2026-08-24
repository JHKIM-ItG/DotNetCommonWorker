using CommonServiceProject;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MultiServiceApp;

ConsoleQuickEditGuard.Disable();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCommonWorker<OrderProcessingWorker>();
builder.Services.AddCommonWorker<DailyReportWorker>();
builder.Services.AddCommonWorker<DatabaseCleanupWorker>();
builder.Services.AddCommonWorker<FileImportWorker>();

// 등록된 모든 워커의 상태를 헬스체크로 노출
builder.Services.AddHealthChecks()
                .AddCheck<WorkerHealthCheck>("workers");

var host = builder.Build();

// 앱 시작 시 등록된 워커 목록 출력
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var workers = host.Services.GetServices<BaseWorkerService>().ToList();
logger.LogInformation("=== 등록된 워커 목록 ({Count}개) ===", workers.Count);
foreach (var worker in workers)
{
    logger.LogInformation("  - {WorkerName}", worker.HealthStatus.WorkerName);
}

host.Run();


