using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transports.Http;
using NServiceBus.Wormhole.Gateway;
using NUnit.Framework;

class GatewayComponent : IComponentBehavior
{
    string siteName;
    Action<WormholeGatewayConfiguration<LearningTransport, HttpTransport>> configAction;

    public GatewayComponent(string siteName, Action<WormholeGatewayConfiguration<LearningTransport, HttpTransport>> configAction)
    {
        this.siteName = siteName;
        this.configAction = configAction;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var testRunId = TestContext.CurrentContext.Test.ID;
        string tempDir;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            //can't use bin dir since that will be too long on the build agents
            tempDir = @"c:\temp";
        }
        else
        {
            tempDir = Path.GetTempPath();
        }

        var storageDir = Path.Combine(tempDir, testRunId);

        var config = new WormholeGatewayConfiguration<LearningTransport, HttpTransport>(siteName, siteName);
        configAction(config);
        config.CustomizeLocalTransport((e, t) => t.StorageDirectory(storageDir));

        var adapter = config.Build();
        
        return Task.FromResult<ComponentRunner>(new Runner(adapter, siteName));
    }

    class Runner : ComponentRunner
    {
        IStartableWormholeGateway gateway;
        IWormholeGateway runningGateway;

        public Runner(IStartableWormholeGateway gateway, string siteName)
        {
            this.gateway = gateway;
            this.Name = siteName;
        }

        public override async Task Start(CancellationToken token)
        {
            runningGateway = await gateway.Start().ConfigureAwait(false);
        }

        public override Task Stop()
        {
            return runningGateway != null 
                ? runningGateway.Stop() 
                : Task.CompletedTask;
        }

        public override string Name { get; }
    }
}