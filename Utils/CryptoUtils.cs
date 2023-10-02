using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace HelpDesk.Utils
{
    public class CryptoUtils
    {
        public static byte[] GetSalt()
        {
            return RandomNumberGenerator.GetBytes(128 / 8);
        }

        public static String HashPassword(String password, byte[] salt)
        {
            return Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password!,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8
                ));
        }
    }
}
