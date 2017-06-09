namespace NServiceBus.Wormhole
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Configuration.AdvanceExtensibility;
    using Settings;

    public class WormholeRoutingSettings : ExposeSettings
    {
        public WormholeRoutingSettings(string gatwayAddress, SettingsHolder settings) : base(settings)
        {
            GatwayAddress = gatwayAddress;
        }

        internal string GatwayAddress { get; }
        internal Dictionary<Type, Func<object, string[]>> RouteTable { get; } = new Dictionary<Type, Func<object, string[]>>();

        /// <summary>
        /// Configures routing of a given message type to the provided sites via the worm hole.
        /// </summary>
        /// <param name="sitesProperty">The property of message that contains the names of the destination sites.</param>
        public WormholeRoutingSettings RouteToSite<T>(Func<T, IEnumerable<string>> sitesProperty)
        {
            if (sitesProperty == null)
            {
                throw new ArgumentNullException(nameof(sitesProperty));
            }

            Func<object, string[]> callback = o =>
            {
                var message = (T) o;
                return sitesProperty(message).ToArray();
            };

            RouteTable[typeof(T)] = callback;
            return this;
        }

        /// <summary>
        /// Configures routing of a given message type to the provided site via the worm hole.
        /// </summary>
        /// <param name="siteProperty">The property of message that contains the name of the destination site.</param>
        public WormholeRoutingSettings RouteToSite<T>(Func<T, string> siteProperty)
        {
            if (siteProperty == null)
            {
                throw new ArgumentNullException(nameof(siteProperty));
            }

            Func<object, string[]> callback = o =>
            {
                var message = (T) o;
                return new[]
                {
                    siteProperty(message)
                };
            };

            RouteTable[typeof(T)] = callback;
            return this;
        }

        /// <summary>
        /// Configures routing of a given message type to the provided site via the worm hole.
        /// </summary>
        /// <param name="destinationSite">The name of the destination site.</param>
        public WormholeRoutingSettings RouteToSite<T>(string destinationSite)
        {
            if (destinationSite == null)
            {
                throw new ArgumentNullException(nameof(destinationSite));
            }

            Func<object, string[]> callback = o => new[]
            {
                destinationSite
            };
            RouteTable[typeof(T)] = callback;
            return this;
        }
    }
}