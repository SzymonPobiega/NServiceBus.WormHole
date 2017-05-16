using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transports.Http;
using NServiceBus.WormHole.Gateway;

class GatewayComponent : IComponentBehavior
{
    string siteName;
    Action<WormHoleGatewayConfiguration<MsmqTransport, HttpTransport>> configAction;

    public GatewayComponent(string siteName, Action<WormHoleGatewayConfiguration<MsmqTransport, HttpTransport>> configAction)
    {
        this.siteName = siteName;
        this.configAction = configAction;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var config = new WormHoleGatewayConfiguration<MsmqTransport, HttpTransport>(siteName, siteName);
        configAction(config);
        var adapter = config.Build();
        
        return Task.FromResult<ComponentRunner>(new Runner(adapter, siteName));
    }

    class Runner : ComponentRunner
    {
        IStartableWormHoleGateway gateway;
        IWormHoleGateway runningGateway;

        public Runner(IStartableWormHoleGateway gateway, string siteName)
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