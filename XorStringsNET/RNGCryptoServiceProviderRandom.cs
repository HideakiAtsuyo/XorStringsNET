using System;
using System.Security.Cryptography;

namespace XorStringsNET
{
    internal static class RNGCryptoServiceProviderRandom
    {
        internal static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        //https://stackoverflow.com/a/6112797
        internal static int GetNextInt32(int maxValue)
        {
            var buffer = new byte[4];
            int bits, val;

            if ((maxValue & -maxValue) == maxValue)
            {
                rng.GetBytes(buffer);
                bits = BitConverter.ToInt32(buffer, 0);
                return bits & (maxValue - 1);
            }

            do
            {
                rng.GetBytes(buffer);
                bits = BitConverter.ToInt32(buffer, 0) & 0x7FFFFFFF;
                val = bits % maxValue;
            } while (bits - val + (maxValue - 1) < 0);
            return val;
        }
    }
}