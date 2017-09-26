namespace ServiceControl.TransportAdapter.AcceptanceTests.Infrastructure
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    public abstract class AcceptanceTestBase
    {
        [SetUp]
        public Task ClearQueues()
        {
            return Cleanup();
        }

        Task Cleanup()
        {
            return Task.FromResult(0);
        }
    }
}