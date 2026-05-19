using FixAcceptor.Fix;
using FixAcceptor.Infrastructure;
using FixAcceptor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

// Anchor cwd to the deployed binary so QuickFIX/n resolves DataDictionary/FileStorePath/FileLogPath
// (all relative in server.cfg) regardless of where the process was launched from.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ISessionSettingsProvider, SessionSettingsProvider>();
builder.Services.AddSingleton<ISessionRegistry, SessionRegistry>();
builder.Services.AddSingleton<IOrderBook, OrderBook>();
builder.Services.AddSingleton<IFixMessageSender, QuickFixMessageSender>();
builder.Services.AddSingleton<IFixMessageHandler, FixMessageHandler>();
builder.Services.AddSingleton<ISecurityUniverse, SecurityUniverse>();
builder.Services.AddSingleton<IMarketDataSubscriptions, MarketDataSubscriptions>();
builder.Services.AddSingleton<IMarketDataHandler, MarketDataHandler>();
builder.Services.AddSingleton<IExecutionFileProcessor, ExecutionFileProcessor>();
builder.Services.AddSingleton<FixServerApp>();
builder.Services.AddHostedService<FixAcceptorHost>();
builder.Services.AddHostedService<ExecutionFileWatcherService>();
builder.Services.AddHostedService<MarketDataPublisher>();

builder.Logging.AddSimpleConsole(options =>
{
    options.ColorBehavior = LoggerColorBehavior.Disabled;
});

try
{
    Console.BackgroundColor = ConsoleColor.Magenta;
    Console.Clear();
}
catch (IOException)
{
    // No console buffer attached (headless run, redirected output) — skip cosmetics.
}
Console.WriteLine("******** FIX Server ****************");

Console.WriteLine($" Using QuickFIX/n version: {typeof(QuickFix.Session).Assembly.GetName().Version}");
Console.WriteLine($" Using directory: {AppContext.BaseDirectory}");
Console.WriteLine($" Using directory executions : {Path.Combine(AppContext.BaseDirectory, "executions")}");
Console.WriteLine($" Using directory processed  : {Path.Combine(AppContext.BaseDirectory, "executions", "processed")}");


var app = builder.Build();
app.Run();
