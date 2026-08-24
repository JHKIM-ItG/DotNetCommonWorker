using CommonServiceProject;
using Microsoft.Extensions.Hosting;
using SingleServiceApp;

var builder = Host.CreateApplicationBuilder(args);

// 단일 워커 서비스 등록
builder.Services.AddCommonWorker<MySingleWorker>();

var host = builder.Build();
host.Run();
