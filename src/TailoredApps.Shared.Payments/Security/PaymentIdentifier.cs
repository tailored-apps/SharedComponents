#nullable enable
using System;

namespace TailoredApps.Shared.Payments.Security
{
    /// <summary>
    /// Validates provider payment / order / transaction identifiers before they are placed into
    /// request URLs sent to a payment gateway, so that a caller-supplied identifier cannot
    /// re-target the request to another gateway endpoint (path traversal, query or fragment injection).
    /// </summary>
    public static class PaymentIdentifier
    {
        /// <summary>Maximum accepted identifier length.</summary>
        public const int MaxLength = 128;

        /// <summary>
        /// Returns <c>true</c> when <paramref name="id"/> is non-empty, at most <see cref="MaxLength"/>
        /// characters long and consists only of ASCII letters, digits, underscore, hyphen and dot
        /// (and is not a single or double dot).
        /// </summary>
        /// <param name="id">The identifier to validate.</param>
        public static bool IsSafe(string? id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > MaxLength)
                return false;

            if (id == "." || id == "..")
                return false;

            foreach (var c in id)
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                         || c == '_' || c == '-' || c == '.';
                if (!ok) return false;
            }

            return true;
        }

        /// <summary>
        /// Validates <paramref name="id"/> with <see cref="IsSafe"/> and returns it URL-escaped,
        /// ready to be used as a single path segment.
        /// </summary>
        /// <param name="id">The identifier to validate and escape.</param>
        /// <param name="paramName">Parameter name reported in the exception.</param>
        /// <exception cref="ArgumentException">Thrown when the identifier is not safe.</exception>
        public static string EnsureSafe(string? id, string paramName = "paymentId")
        {
            if (!IsSafe(id))
                throw new ArgumentException("Payment identifier contains unsupported characters or is too long.", paramName);

            return Uri.EscapeDataString(id!);
        }
    }
}
