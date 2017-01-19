using System;

namespace NServiceBus.WormHole.Gateway
{
    public class MessageTypeRange : MessageTypeSpecification
    {
        public string Namespace { get; }
        public string Assembly { get; }

        public override bool OverlapsWith(MessageTypeRange spec)
        {
            return Assembly == spec.Assembly && NamespacesOverlap(Namespace, spec.Namespace);
        }

        static bool NamespacesOverlap(string thisNamespace, string otherNamespace)
        {
            if (thisNamespace == otherNamespace)
            {
                return true;
            }
            if (string.IsNullOrEmpty(thisNamespace) || string.IsNullOrEmpty(otherNamespace))
            {
                return false;
            }
            return thisNamespace.StartsWith(otherNamespace) || otherNamespace.StartsWith(thisNamespace);
        }

        public override bool OverlapsWith(MessageType spec)
        {
            return spec.Assembly == Assembly 
                && (Namespace == null || spec.Namespace == Namespace);
        }

        public MessageTypeRange(string ns, string assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }
            Namespace = ns;
            Assembly = assembly;
        }

        protected bool Equals(MessageTypeRange other)
        {
            return string.Equals(Namespace, other.Namespace) && string.Equals(Assembly, other.Assembly);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MessageTypeRange)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Namespace != null ? Namespace.GetHashCode() : 0) * 397) ^ (Assembly != null ? Assembly.GetHashCode() : 0);
            }
        }
    }
}