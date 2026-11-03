using System;
using System.Security.Cryptography;

namespace Knx.EtsBridge.Addin
{
    /// <summary>
    /// Session-token helpers for the IPC endpoint (named pipe + optional TCP).
    ///
    /// The bridge ALWAYS authenticates: an empty "Token" preference no longer means
    /// "no authentication" but "generate a random token". The named-pipe client reads
    /// it from session.json; a remote (TCP) client copies it from the AddIn panel.
    ///
    /// Pure (no ETS SDK / WPF dependency) so it is unit-testable on any platform.
    /// </summary>
    internal static class BridgeToken
    {
        /// <summary>256 bits of entropy.</summary>
        private const int TokenBytes = 32;

        /// <summary>Generates a cryptographically random token (64 lowercase hex chars).</summary>
        public static string Generate()
        {
            var bytes = new byte[TokenBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Returns the configured token (trimmed) or, when it is empty/whitespace,
        /// a freshly generated random token.
        /// </summary>
        public static string Resolve(string? configured)
        {
            return string.IsNullOrWhiteSpace(configured) ? Generate() : configured!.Trim();
        }

        /// <summary>
        /// Constant-time string comparison: the running time depends only on the
        /// length of <paramref name="expected"/>, not on where the first mismatching
        /// character is, so the token cannot be recovered byte by byte through timing.
        /// An empty expected token never matches (there is no "no auth" mode).
        /// </summary>
        public static bool FixedTimeEquals(string? expected, string? actual)
        {
            if (string.IsNullOrEmpty(expected) || actual == null)
                return false;

            int diff = expected!.Length ^ actual.Length;
            for (int i = 0; i < expected.Length; i++)
            {
                char candidate = i < actual.Length ? actual[i] : '\0';
                diff |= expected[i] ^ candidate;
            }
            return diff == 0;
        }
    }
}
