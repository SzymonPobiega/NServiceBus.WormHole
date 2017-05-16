using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.WormHole;
using NServiceBus.WormHole.Gateway;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_deriving_destination_site_from_message_content : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_message()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new GatewayComponent("SiteA", c =>
            {
                c.ConfigureRemoteSite("SiteB", "SiteB");
                c.ConfigureRemoteSite("SiteC", "SiteC");
            }))
            .WithComponent(new GatewayComponent("SiteB", c =>
            {
                c.ForwardToEndpoint(MessageType.Parse(typeof(MyMessage).AssemblyQualifiedName), 
                    Conventions.EndpointNamingConvention(typeof(ReceiverA)));
            }))
            .WithComponent(new GatewayComponent("SiteC", c =>
            {
                c.ForwardToEndpoint(MessageType.Parse(typeof(MyMessage).AssemblyQualifiedName),
                    Conventions.EndpointNamingConvention(typeof(ReceiverB)));
            }))
            .WithEndpoint<Sender>(c => c.When(async s =>
            {
                await s.Send(new MyMessage
                {
                    Site = "SiteB"
                }).ConfigureAwait(false);
                await s.Send(new MyMessage
                {
                    Site = "SiteC"
                }).ConfigureAwait(false);
            }))
            .WithEndpoint<ReceiverA>()
            .WithEndpoint<ReceiverB>()
            .Done(c => c.ReceivedA && c.ReceivedB)
            .Run();

        Assert.IsTrue(result.ReceivedA);
        Assert.IsTrue(result.ReceivedB);
    }

    class Context : ScenarioContext
    {
        public bool ReceivedA { get; set; }
        public bool ReceivedB { get; set; }
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.UseWormHoleGateway("SiteA").RouteToSite<MyMessage>(m => m.Site);
            });
        }
    }

    class ReceiverA : EndpointConfigurationBuilder
    {
        public ReceiverA()
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
                scenarioContext.ReceivedA = true;
                return Task.CompletedTask;
            }
        }
    }

    class ReceiverB : EndpointConfigurationBuilder
    {
        public ReceiverB()
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
                scenarioContext.ReceivedB = true;
                return Task.CompletedTask;
            }
        }
    }

    class MyMessage : IMessage
    {
        public string Site { get; set; }
    }
}
