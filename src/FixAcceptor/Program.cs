using FixAcceptor.Fix;
using FixAcceptor.Infrastructure;
using FixAcceptor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ISessionSettingsProvider, SessionSettingsProvider>();
builder.Services.AddSingleton<ISessionRegistry, SessionRegistry>();
builder.Services.AddSingleton<IFixMessageHandler, FixMessageHandler>();
builder.Services.AddSingleton<IExecutionFileProcessor, ExecutionFileProcessor>();
builder.Services.AddSingleton<FixServerApp>();
builder.Services.AddHostedService<FixAcceptorHost>();
builder.Services.AddHostedService<ExecutionFileWatcherService>();

builder.Logging.AddSimpleConsole(options =>
{
    options.ColorBehavior = LoggerColorBehavior.Disabled;
});

// Set background color to purple
Console.BackgroundColor = ConsoleColor.Magenta; // Closest to purple

// Clear the console to apply the color to the entire screen
Console.Clear();
Console.WriteLine("******** FIX Client ****************");

var app = builder.Build();
app.Run();
