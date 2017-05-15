using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Routing;
using NServiceBus.WormHole;
using NServiceBus.WormHole.Gateway;

namespace Demo
{
    using NServiceBus.Transports.Http;

    class Program
    {
        const string SqlConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;";

        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var endpointA = await StartEndpointA();
            var endpointB = await StartEndpointB();
            var gatewayA = await StartGatewayA();
            var gatewayB = await StartGatewayB();

            while (true)
            {
                Console.WriteLine("Press <enter> to send a message");
                Console.ReadLine();

                await endpointA.Send(new MyMessage
                {
                    Destination = "SiteB"
                });
            }
        }

        static Task<IWormHoleGateway> StartGatewayB()
        {
            var config = new WormHoleGatewayConfiguration<MsmqTransport, HttpTransport>("WormHole-B", "SiteB");
            config.ConfigureRemoteSite("SiteA", "WormHole-A");
            config.CustomizeLocalTransport((c, t) =>
            {
                c.AutoCreateQueue();
            });
            config.ForwardToEndpoint(new MessageType("MyMessage", "Demo", "Demo"), "EndpointB");

            return config.Start();
        }

        static Task<IWormHoleGateway> StartGatewayA()
        {
            var config = new WormHoleGatewayConfiguration<SqlServerTransport, HttpTransport>("WormHole-A", "SiteA");
            config.ConfigureRemoteSite("SiteB", "WormHole-B");
            config.CustomizeLocalTransport((c, t) =>
            {
                t.GetSettings().Set<EndpointInstances>(config.EndpointInstances); //SQL transport requires this :(
                t.ConnectionString(SqlConnectionString);
                c.AutoCreateQueue();
            });

            return config.Start();
        }

        static async Task<IEndpointInstance> StartEndpointB()
        {
            var config = PrepareCommonConfig("EndpointB");
            config.UseTransport<MsmqTransport>();

            return await Endpoint.Start(config);
        }

        static async Task<IEndpointInstance> StartEndpointA()
        {
            var config = PrepareCommonConfig("EndpointA");
            config.UseTransport<SqlServerTransport>().ConnectionString(SqlConnectionString);

            var wormHoleSettings = config.UseWormHoleGateway("WormHole-A");
            wormHoleSettings.RouteToSite<MyMessage>(m => m.Destination);

            return await Endpoint.Start(config);
        }

        static EndpointConfiguration PrepareCommonConfig(string name)
        {
            var endpointAConfig = new EndpointConfiguration(name);
            endpointAConfig.UsePersistence<InMemoryPersistence>();
            endpointAConfig.SendFailedMessagesTo("error");
            return endpointAConfig;
        }
    }
}
