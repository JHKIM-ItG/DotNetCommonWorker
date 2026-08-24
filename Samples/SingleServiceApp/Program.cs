using CommonServiceProject;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SingleServiceApp;

ConsoleQuickEditGuard.Disable();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCommonWorker<MySingleWorker>();

var host = builder.Build();
host.Run();
