using System.Collections.Concurrent;
using NServiceBus.Routing;

namespace NServiceBus.WormHole.Gateway
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    public class RoutingTable
    {
        internal RouteGroup[] GetRoutesFor(MessageType messageType)
        {
            RouteGroup[] routes;
            return routeTable.TryGetValue(messageType, out routes)
                ? routes
                : UpdateCache(messageType);
        }

        RouteGroup[] UpdateCache(MessageType messageType)
        {
            lock (routeTableLock)
            {
                knownMessageTypes.Add(messageType);
                routeTable = CalculateNewRouteTable();
            }
            return routeTable[messageType];
        }

        public void AddOrReplaceRoutes(string sourceKey, IList<RoutingTableEntry> entries)
        {
            // The algorithm uses ReaderWriterLockSlim. First entries are read. If then exists they are compared with passed entries and skipped if equal.
            // Otherwise, the write path is used. It's possible than one thread will execute all the work
            var existing = GetExistingRoutes(sourceKey);
            if (existing != null && existing.SequenceEqual(entries))
            {
                return;
            }

            routeGroupsLock.EnterWriteLock();
            try
            {
                routeGroups[sourceKey] = entries;
                lock (routeTableLock)
                {
                    routeTable = CalculateNewRouteTable();
                }
            }
            finally
            {
                routeGroupsLock.ExitWriteLock();
            }
        }

        IList<RoutingTableEntry> GetExistingRoutes(string sourceKey)
        {
            routeGroupsLock.EnterReadLock();
            try
            {
                IList<RoutingTableEntry> existing;
                routeGroups.TryGetValue(sourceKey, out existing);
                return existing;
            }
            finally
            {
                routeGroupsLock.ExitReadLock();
            }
        }

        Dictionary<MessageType, RouteGroup[]> CalculateNewRouteTable()
        {
            var allEntries = routeGroups.Values.SelectMany(g => g).ToArray();

            var newRouteTable = knownMessageTypes.ToDictionary(x => x, x =>
            {
                return allEntries.Where(e => e.MessageTypeSpec.OverlapsWith(x)).Select(e => e.Route).ToList();
            });
            
            return newRouteTable.ToDictionary(kvp => kvp.Key, kvp => GroupByEndpoint(kvp.Value));
        }

        static RouteGroup[] GroupByEndpoint(List<UnicastRoute> routes)
        {
            return routes.GroupBy(r => r.Endpoint)
                .Select(g => new RouteGroup(g.Key, g.ToArray()))
                .ToArray();
        }

        object routeTableLock = new object();
        ReaderWriterLockSlim routeGroupsLock = new ReaderWriterLockSlim();

        HashSet<MessageType> knownMessageTypes = new HashSet<MessageType>();
        volatile Dictionary<MessageType, RouteGroup[]> routeTable = new Dictionary<MessageType, RouteGroup[]>();
        Dictionary<string, IList<RoutingTableEntry>> routeGroups = new Dictionary<string, IList<RoutingTableEntry>>();
    }
}