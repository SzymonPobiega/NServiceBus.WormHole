using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Wormhole;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_forwarding_all_messages_in_assembly : NServiceBusAcceptanceTest
{
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
                c.ForwardToEndpoint("NServiceBus.Wormhole.AcceptanceTests", 
                    Conventions.EndpointNamingConvention(typeof(Receiver)));
            }))
            .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyMessage())))
            .WithEndpoint<Receiver>()
            .Done(c => c.Received)
            .Run();

        Assert.IsTrue(result.Received);
    }

    class Context : ScenarioContext
    {
        public bool Received { get; set; }
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

            public MyMessageHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                scenarioContext.Received = true;
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : IMessage
    {
    }
}
