using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Settings;

namespace NServiceBus.WormHole
{
    public class WormHoleRoutingSettings : ExposeSettings
    {
        internal string GatwayAddress { get; }
        internal Dictionary<Type, HashSet<string>> RouteTable { get; } = new Dictionary<Type, HashSet<string>>();

        public WormHoleRoutingSettings(string gatwayAddress, SettingsHolder settings) : base(settings)
        {
            GatwayAddress = gatwayAddress;
        }

        /// <summary>
        /// Configures routing of a given message type to the provided site via the worm hole.
        /// </summary>
        /// <param name="messageType">Type of message.</param>
        /// <param name="destinationSites">Destination site.</param>
        public WormHoleRoutingSettings RouteToSites(Type messageType, params string[] destinationSites)
        {
            if (messageType == null)
            {
                throw new ArgumentNullException(nameof(messageType));
            }
            if (destinationSites == null)
            {
                throw new ArgumentNullException(nameof(destinationSites));
            }
            if (destinationSites.Length == 0)
            {
                throw new ArgumentException("Site list cannot be empty.", nameof(destinationSites));
            }

            HashSet<string> sites;
            if (!RouteTable.TryGetValue(messageType, out sites))
            {
                sites = new HashSet<string>();
                RouteTable[messageType] = sites;
            }
            foreach (var site in destinationSites)
            {
                if (site.Contains(";"))
                {
                    throw new Exception($"Site name cannot contain a semicolon: {site}.");
                }
                sites.Add(site);
            }
            return this;
        }
    }
}
