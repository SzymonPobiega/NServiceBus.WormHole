using System;

namespace NServiceBus.WormHole.Gateway
{
    /// <summary>
    /// Represents a CLR type.
    /// </summary>
    public class MessageType : MessageTypeSpecification
    {
        /// <summary>
        /// Name of the type.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Namespace. Empty string is type has no namespace.
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        /// Assembly name
        /// </summary>
        public string Assembly { get; }

        public static MessageType Parse(string assemblyQualifiedName)
        {
            var parts = assemblyQualifiedName.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                throw new Exception("Assembly qualified type name consists of a full name and assembly name separated by a comma (,).");
            }
            var nameAndNamespace = parts[0].Trim();
            var assembly = parts[1].Trim();

            var lastDotIndex = nameAndNamespace.LastIndexOf(".", StringComparison.Ordinal);
            if (lastDotIndex < 0)
            {
                return new MessageType(nameAndNamespace, "", assembly);
            }
            var ns = nameAndNamespace.Substring(0, lastDotIndex);
            var name = nameAndNamespace.Substring(lastDotIndex + 1);
            return new MessageType(name, ns, assembly);
        }

        public MessageType(string name, string ns, string assembly)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (ns == null)
            {
                throw new ArgumentNullException(nameof(ns));
            }
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }
            Name = name;
            Namespace = ns;
            Assembly = assembly;
        }

        public override bool OverlapsWith(MessageTypeRange spec)
        {
            return spec.OverlapsWith(this);
        }

        public override bool OverlapsWith(MessageType spec)
        {
            return spec.Equals(this);
        }

        protected bool Equals(MessageType other)
        {
            return string.Equals(Name, other.Name) && string.Equals(Namespace, other.Namespace) && string.Equals(Assembly, other.Assembly);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MessageType) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name.GetHashCode();
                hashCode = (hashCode*397) ^ Namespace.GetHashCode();
                hashCode = (hashCode*397) ^ Assembly.GetHashCode();
                return hashCode;
            }
        }
    }
}