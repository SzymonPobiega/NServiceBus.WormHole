using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NServiceBus.WormHole.Gateway
{
    static class TypeLengthValueEncoder
    {
        public static string EncodeTLV(this Dictionary<string, string> values)
        {
            return string.Join("|", values.Select(kvp => $"{kvp.Key}|{kvp.Value.Length}|{kvp.Value}"));
        }

        public static Dictionary<string, string> DecodeTLV(this string tlvString)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            var remaining = tlvString;
            while (true)
            {
                var next = remaining.IndexOf("|", StringComparison.Ordinal);
                if (next < 0)
                {
                    throw new Exception("Expected type");
                }
                var type = remaining.Substring(0, next);
                remaining = remaining.Substring(next + 1);

                next = remaining.IndexOf("|", StringComparison.Ordinal);
                if (next < 0)
                {
                    throw new Exception("Expected length");
                }
                var lengthString = remaining.Substring(0, next);
                var length = int.Parse(lengthString);

                remaining = remaining.Substring(next + 1);
                if (remaining.Length < length)
                {
                    throw new Exception($"Expected content of {length} characters");
                }

                var value = remaining.Substring(0, length);
                result[type] = value;

                remaining = remaining.Substring(length);
                if (remaining == "")
                {
                    return result;
                }
                if (!remaining.StartsWith("|"))
                {
                    throw new Exception($"Expected separator");
                }
                remaining = remaining.Substring(1);
            }
        }
    }
}