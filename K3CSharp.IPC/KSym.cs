using System;

namespace K3CSharp.IPC
{
    /// <summary>
    /// Wrapper for a K3 symbol (e.g. <c>`abc</c>). Distinguishes a symbol from
    /// a regular string when encoding or decoding IPC messages.
    /// </summary>
    public readonly struct KSym : IEquatable<KSym>
    {
        /// <summary>The symbol text (without the leading back-tick).</summary>
        public string Name { get; }

        /// <summary>Create a new symbol with the given name.</summary>
        public KSym(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>Render the symbol in K3 source form, e.g. <c>`abc</c>.</summary>
        public override string ToString() => "`" + Name;

        /// <inheritdoc/>
        public bool Equals(KSym other) => Name == other.Name;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is KSym other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Name?.GetHashCode(StringComparison.Ordinal) ?? 0;

        /// <summary>Compare two symbols for equality.</summary>
        public static bool operator ==(KSym left, KSym right) => left.Equals(right);

        /// <summary>Compare two symbols for inequality.</summary>
        public static bool operator !=(KSym left, KSym right) => !left.Equals(right);

        /// <summary>Convenience constructor used by the codec and tests.</summary>
        public static KSym Of(string name) => new KSym(name);
    }
}
