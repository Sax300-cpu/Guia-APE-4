using System.Security.Cryptography;
using LogiMarket.Application.Puertos;

namespace LogiMarket.Infrastructure.Seguridad;

/// <summary>PBKDF2-SHA256 con sal aleatoria. Formato: pbkdf2$iteraciones$sal$hash (Base64).</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iteraciones = 210_000;
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
        return $"pbkdf2${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string password, string hash)
    {
        var partes = hash.Split('$');
        if (partes.Length != 4 || partes[0] != "pbkdf2" || !int.TryParse(partes[1], out var iteraciones)) return false;
        var sal = Convert.FromBase64String(partes[2]);
        var esperado = Convert.FromBase64String(partes[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(actual, esperado);
    }
}