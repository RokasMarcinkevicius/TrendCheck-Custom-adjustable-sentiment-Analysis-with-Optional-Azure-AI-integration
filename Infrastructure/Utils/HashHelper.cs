using System;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Utils;

public static class HashHelper
{
    public static string Sha1(string input)
    {
        using var sha1 = SHA1.Create();
        var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}