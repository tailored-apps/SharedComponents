#nullable enable
using System.Security.Cryptography;
using System.Text;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Compares payment signatures (HMACs, hashes, checksums) in constant time.
    /// </summary>
    /// <remarks>
    /// An ordinary string comparison returns as soon as it finds the first differing character, so the
    /// time it takes reveals how much of a forged signature was correct. Payment providers use this type
    /// when they verify the signature of an incoming notification, and custom providers should do the same.
    /// </remarks>
    public static class SignatureComparer
    {
        /// <summary>
        /// Determines whether the signature computed locally equals the signature received from the caller,
        /// taking the same amount of time wherever the two values differ.
        /// </summary>
        /// <param name="expected">The signature computed locally from the shared secret.</param>
        /// <param name="actual">The signature received with the request.</param>
        /// <param name="ignoreCase">
        /// <see langword="true"/> to ignore casing, for hex-encoded signatures; <see langword="false"/>
        /// for case-sensitive encodings such as Base64.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when both values are present and equal; otherwise <see langword="false"/>.
        /// A missing or empty value on either side is never treated as a match.
        /// </returns>
        public static bool FixedTimeEquals(string? expected, string? actual, bool ignoreCase = false)
        {
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual))
            {
                return false;
            }

            if (ignoreCase)
            {
                expected = expected.ToUpperInvariant();
                actual = actual.ToUpperInvariant();
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(actual));
        }
    }
}
