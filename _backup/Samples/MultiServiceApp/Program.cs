using CommonServiceProject;
using Microsoft.Extensions.Hosting;
using MultiServiceApp.Workers;

var builder = Host.CreateApplicationBuilder(args);

// 다중 워커 서비스 동시 등록 (한 프로그램 내에서 3개 동시 구동)
builder.Services.AddCommonWorker<OrderProcessingWorker>();
builder.Services.AddCommonWorker<DailyReportWorker>();
builder.Services.AddCommonWorker<DatabaseCleanupWorker>();

var host = builder.Build();
host.Run();
