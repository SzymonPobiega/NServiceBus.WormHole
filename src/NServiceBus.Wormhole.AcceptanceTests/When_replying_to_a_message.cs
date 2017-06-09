using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Wormhole;
using NServiceBus.Wormhole.Gateway;
using NUnit.Framework;
using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

[TestFixture]
public class When_replying_to_a_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_the_reply_without_explicit_routing()
    {
        var result = await Scenario.Define<Context>()
            .WithComponent(new GatewayComponent("SiteA", c =>
            {
                c.ConfigureRemoteSite("SiteB", "SiteB");
            }))
            .WithComponent(new GatewayComponent("SiteB", c =>
            {
                c.ConfigureRemoteSite("SiteA", "SiteA");
                c.ForwardToEndpoint(MessageType.Parse(typeof(MyRequest).AssemblyQualifiedName), 
                    Conventions.EndpointNamingConvention(typeof(Receiver)));
            }))
            .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyRequest())))
            .WithEndpoint<Receiver>()
            .Done(c => c.RequestReceived && c.ResponseReceived)
            .Run();

        Assert.IsTrue(result.RequestReceived);
        Assert.IsTrue(result.ResponseReceived);
    }

    class Context : ScenarioContext
    {
        public bool RequestReceived { get; set; }
        public bool ResponseReceived { get; set; }
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.UseWormholeGateway("SiteA").RouteToSite<MyRequest>("SiteB");
            });
        }

        class MyResponseHandler : IHandleMessages<MyResponse>
        {
            Context scenarioContext;

            public MyResponseHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyResponse response, IMessageHandlerContext context)
            {
                scenarioContext.ResponseReceived = true;
                return Task.CompletedTask;
            }
        }
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.UseWormholeGateway("SiteB");
            });
        }

        class MyRequestHandler : IHandleMessages<MyRequest>
        {
            Context scenarioContext;

            public MyRequestHandler(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Handle(MyRequest request, IMessageHandlerContext context)
            {
                scenarioContext.RequestReceived = true;
                return context.Reply(new MyResponse());
            }
        }
    }

    class MyRequest : IMessage
    {
    }

    class MyResponse : IMessage
    {
    }
}
