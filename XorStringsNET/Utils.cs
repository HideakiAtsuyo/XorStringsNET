namespace XorStringsNET
{
    internal static class Utils
    {
        internal static string Remove(this string x, string y) => x.Replace(y, string.Empty);
    }
}