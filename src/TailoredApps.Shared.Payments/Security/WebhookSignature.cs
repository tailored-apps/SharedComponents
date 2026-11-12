#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace TailoredApps.Shared.Payments.Security
{
    /// <summary>
    /// Helpers for verifying payment-gateway webhook signatures safely.
    /// All comparisons run in constant time so that a remote caller cannot learn the expected
    /// signature byte-by-byte from response timing, and every helper fails closed when either
    /// side of the comparison is missing.
    /// </summary>
    public static class WebhookSignature
    {
        /// <summary>
        /// Compares two signature strings (hex, base64 or any other textual encoding) in constant time.
        /// Returns <c>false</c> when either value is <c>null</c> or empty.
        /// </summary>
        /// <param name="expected">The signature computed locally from the configured secret.</param>
        /// <param name="received">The signature supplied by the remote caller.</param>
        public static bool FixedTimeEquals(string? expected, string? received)
        {
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(received))
                return false;

            var a = Encoding.UTF8.GetBytes(expected);
            var b = Encoding.UTF8.GetBytes(received);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        /// <summary>
        /// Constant-time, case-insensitive comparison intended for hex-encoded digests
        /// where gateways may send upper- or lower-case characters.
        /// Returns <c>false</c> when either value is <c>null</c> or empty.
        /// </summary>
        /// <param name="expected">The signature computed locally from the configured secret.</param>
        /// <param name="received">The signature supplied by the remote caller.</param>
        public static bool FixedTimeEqualsIgnoreCase(string? expected, string? received)
        {
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(received))
                return false;

            return FixedTimeEquals(expected.ToLowerInvariant(), received.ToLowerInvariant());
        }

        /// <summary>
        /// Returns <c>true</c> when a secret has been configured (non-empty, non-whitespace).
        /// Providers must refuse to verify signatures when this returns <c>false</c>; hashing with an
        /// empty key would otherwise let anyone forge a valid-looking signature.
        /// </summary>
        /// <param name="secret">The configured secret, HMAC key or CRC value.</param>
        public static bool IsSecretConfigured(string? secret) => !string.IsNullOrWhiteSpace(secret);
    }
}
