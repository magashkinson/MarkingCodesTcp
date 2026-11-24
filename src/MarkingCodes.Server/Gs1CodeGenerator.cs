namespace MarkingCodes.Server;

// формат ЧЗ: 01 GTIN + 21 серийник + GS + 91 ключ + GS + 92 подпись
public sealed class Gs1CodeGenerator(Random random)
{
    public const char GroupSeparator = '\u001D';
    public const int SerialLength = 13;
    public const int KeyIdLength = 4;
    public const int SignatureLength = 44;

    private const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public Gs1CodeGenerator() : this(Random.Shared) { }

    public string Generate() =>
        $"01{GenerateGtin()}21{RandomString(SerialLength)}{GroupSeparator}" +
        $"91{RandomString(KeyIdLength)}{GroupSeparator}92{RandomString(SignatureLength)}";

    public string GenerateGtin()
    {
        Span<char> digits = stackalloc char[13];
        for (var i = 0; i < digits.Length; i++)
            digits[i] = (char)('0' + random.Next(10));
        return string.Concat(digits, [CheckDigit(digits)]);
    }

    // контрольная цифра GS1, веса 3,1,3,1... справа налево
    public static char CheckDigit(ReadOnlySpan<char> data)
    {
        var sum = 0;
        for (var i = 0; i < data.Length; i++)
        {
            var weight = (data.Length - i) % 2 == 1 ? 3 : 1;
            sum += (data[i] - '0') * weight;
        }
        return (char)('0' + (10 - sum % 10) % 10);
    }

    private string RandomString(int length) =>
        string.Create(length, random, static (span, rnd) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = Alphanumeric[rnd.Next(Alphanumeric.Length)];
        });
}
