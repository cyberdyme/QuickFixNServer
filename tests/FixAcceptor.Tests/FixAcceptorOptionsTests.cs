using Microsoft.Extensions.Configuration;
using FixAcceptor.Infrastructure;

namespace FixAcceptor.Tests;

public class FixAcceptorOptionsTests
{
    [Fact]
    public void Defaults_AreSensibleWhenNothingIsConfigured()
    {
        var opts = new FixAcceptorOptions();

        Assert.Equal("server.cfg", opts.ConfigFile);
        Assert.Equal("executions", opts.ExecutionsDirectory);
        Assert.Equal("executions/processed", opts.ProcessedDirectory);
        Assert.Equal(1.0, opts.MarketDataTickIntervalSeconds);
    }

    [Fact]
    public void BindsFromConfigurationSection()
    {
        var dict = new Dictionary<string, string?>
        {
            ["FixAcceptor:ConfigFile"] = "other.cfg",
            ["FixAcceptor:ExecutionsDirectory"] = "inbox",
            ["FixAcceptor:ProcessedDirectory"] = "inbox/done",
            ["FixAcceptor:MarketDataTickIntervalSeconds"] = "2.5",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var opts = new FixAcceptorOptions();
        config.GetSection(FixAcceptorOptions.SectionName).Bind(opts);

        Assert.Equal("other.cfg", opts.ConfigFile);
        Assert.Equal("inbox", opts.ExecutionsDirectory);
        Assert.Equal("inbox/done", opts.ProcessedDirectory);
        Assert.Equal(2.5, opts.MarketDataTickIntervalSeconds);
    }
}
