using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CSorbit
{
    internal sealed class ReferenceIdentityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceIdentityComparer Instance = new ReferenceIdentityComparer();

        private ReferenceIdentityComparer() { }

        public new bool Equals(object first, object second)
        {
            return ReferenceEquals(first, second);
        }

        public int GetHashCode(object value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }
    }
}
