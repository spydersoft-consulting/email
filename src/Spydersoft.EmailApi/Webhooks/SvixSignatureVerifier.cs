using System.Security.Cryptography;
using System.Text;

namespace Spydersoft.EmailApi.Webhooks;

public static class SvixSignatureVerifier
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    private const string SecretPrefix = "whsec_";

    public static bool Verify(string secret, string id, string timestamp, string signatureHeader, string body, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        if (!long.TryParse(timestamp, out var seconds))
        {
            return false;
        }

        if ((now - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration() > Tolerance)
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(secret.StartsWith(SecretPrefix, StringComparison.Ordinal) ? secret[SecretPrefix.Length..] : secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}"));

        foreach (var part in signatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split(',', 2);
            if (pieces.Length != 2 || pieces[0] != "v1")
            {
                continue;
            }

            try
            {
                if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(pieces[1])))
                {
                    return true;
                }
            }
            catch (FormatException)
            {
            }
        }

        return false;
    }

    public static string Sign(string secret, string id, string timestamp, string body)
    {
        var key = Convert.FromBase64String(secret.StartsWith(SecretPrefix, StringComparison.Ordinal) ? secret[SecretPrefix.Length..] : secret);
        return "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}")));
    }
}
