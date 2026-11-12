#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Marks a closed type hierarchy (C# 15 <c>closed</c>). .NET 11 ships this attribute; the copy below lets the
/// <c>net10.0</c> asset declare closed types too. It is internal, so it never clashes with another assembly's copy.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class IsClosedTypeAttribute : Attribute
{
    /// <summary>The types that derive directly from the closed type.</summary>
    public Type[] DerivedTypes { get; set; } = [];
}
#endif
