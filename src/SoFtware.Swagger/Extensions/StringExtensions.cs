using System.Linq;

namespace SoFtware.Swagger.Extensions
{
    public static class StringExtensions
    {
        public static string EnsureBeginSlash(this string withOrWithoutSlash)
        {
            return withOrWithoutSlash.StartsWith("/") ? withOrWithoutSlash : $"/{withOrWithoutSlash}";
        }

        public static string Join(this string[] s, char c)
        {
            return string.Join(c, s);
        }

        public static string[] GetSegments(this string s)
        {
            return s.Split("/")
                .Select(i => i.Trim('/'))
                .Where(i => !string.IsNullOrEmpty(i))
                .ToArray();
        }
    }
}