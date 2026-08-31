using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HealthGoalsTracker.Functions.Services;

public class CursorCodec
{
    public byte[] SigningKey { get; }

    public CursorCodec(string signingKey)
    {
        if (signingKey.Length < 32)
            throw new ArgumentException("Cursor signing key must contain at least 32 characters.");
        SigningKey = Encoding.UTF8.GetBytes(signingKey);
    }

    public string Encode(string subject, long sequence)
    {
        var payload = $"v1:{sequence.ToString(CultureInfo.InvariantCulture)}:{SubjectHash(subject)}";
        var signature = HMACSHA256.HashData(SigningKey, Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(Encoding.UTF8.GetBytes(payload)) + "." + Base64UrlEncode(signature);
    }

    public bool TryDecode(string subject, string? cursor, out long sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        try
        {
            var parts = cursor.Split('.');
            if (parts.Length != 2)
                return false;
            var payloadBytes = Base64UrlDecode(parts[0]);
            var suppliedSignature = Base64UrlDecode(parts[1]);
            var expectedSignature = HMACSHA256.HashData(SigningKey, payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
                return false;

            var payload = Encoding.UTF8.GetString(payloadBytes);
            var values = payload.Split(':');
            return values.Length == 3 &&
                   values[0] == "v1" &&
                   values[2] == SubjectHash(subject) &&
                   long.TryParse(
                       values[1],
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out sequence) &&
                   sequence >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string SubjectHash(string subject) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)));

    public static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
