using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Routing;
using NServiceBus.Settings;
using NServiceBus.Wormhole;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_using_sender_side_distribution : NServiceBusAcceptanceTest
{
    string ReceiverEndpoint => Conventions.EndpointNamingConvention(typeof(Receiver));

    [Test]
    public async Task Should_deliver_the_message()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new GatewayComponent("SiteA", c =>
            {
                c.ConfigureRemoteSite("SiteB", "SiteB");
            }))
            .WithComponent(new GatewayComponent("SiteB", c =>
            {
                c.ForwardToEndpoint("NServiceBus.Wormhole.AcceptanceTests", ReceiverEndpoint);
                c.EndpointInstances.AddOrReplaceInstances("key", new List<EndpointInstance>
                {
                    new EndpointInstance(ReceiverEndpoint, "1"),
                    new EndpointInstance(ReceiverEndpoint, "2")
                });
            }))
            .WithEndpoint<Sender>(c => c.When(async s =>
            {
                await s.Send(new MyMessage()).ConfigureAwait(false);
                await s.Send(new MyMessage()).ConfigureAwait(false);
            }))
            .WithEndpoint<Receiver>(c => c.CustomConfig(cfg => cfg.MakeInstanceUniquelyAddressable("1")))
            .WithEndpoint<Receiver>(c => c.CustomConfig(cfg => cfg.MakeInstanceUniquelyAddressable("2")))
            .Done(c => c.Received.Count >= 2)
            .Run();

        Assert.IsTrue(result.Received.Count >= 2);
    }

    class Context : ScenarioContext
    {
        public ConcurrentDictionary<string, int> Received { get; } = new ConcurrentDictionary<string, int>();
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.UseWormholeGateway("SiteA").RouteToSite<MyMessage>("SiteB");
            });
        }
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver()
        {
            EndpointSetup<DefaultServer>();
        }

        class MyMessageHandler : IHandleMessages<MyMessage>
        {
            Context scenarioContext;
            ReadOnlySettings settings;

            public MyMessageHandler(Context scenarioContext, ReadOnlySettings settings)
            {
                this.scenarioContext = scenarioContext;
                this.settings = settings;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                var discriminator = settings.Get<string>("EndpointInstanceDiscriminator");
                scenarioContext.Received.AddOrUpdate(discriminator, 1, (k, v) => v + 1);
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}
