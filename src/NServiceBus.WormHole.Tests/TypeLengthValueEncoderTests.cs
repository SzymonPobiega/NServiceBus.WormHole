using System.Collections.Generic;
using NServiceBus.WormHole.Gateway;
using NUnit.Framework;

namespace NServiceBus.WormHole.Tests
{
    [TestFixture]
    public class TypeLengthValueEncoderTests
    {
        [Test]
        public void It_can_encode_and_decode()
        {
            var data = new Dictionary<string, string>
            {
                ["key1"] = "valueA",
                ["key2"] = "valueB",
                ["key3"] = "valueC"
            };

            var result = data.EncodeTLV().DecodeTLV();

            CollectionAssert.AreEquivalent(data, result);
        }
    }
}
