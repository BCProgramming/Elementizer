using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BASeCamp.Elementizer
{
    public static class EnumExtensions
    {
        public static IEnumerable<T> ToValues<T>(this T flags) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an Enum");

            int lookfor = (int)(object)flags;
            foreach (T iterate in Enum.GetValues(typeof(T)))
            {
                int intval = (int)(object)iterate;
                if (0 != (intval & lookfor))
                {
                    yield return iterate;
                }
            }
        }
        public static T ToMask<T>(this IEnumerable<T> values) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an Enum");

            int Result = 0;
            foreach (T checkMask in Enum.GetValues(typeof(T)))
            {
                if (values.Contains(checkMask))
                {
                    Result |= Convert.ToInt32(checkMask);
                }
            }
            return (T)Enum.Parse(typeof(T), Result.ToString());
        }
        public static String ToFlagString<T>(T flags) where T:struct,IConvertible
        {
            if(!typeof(T).IsEnum) throw new ArgumentException("T must be an Enum");
            return String.Join(",", from p in flags.ToValues() select Enum.GetName(typeof(T),p));
        }
        public static T FromFlagString<T>(String source) where T:struct,IConvertible
        {
            if (String.IsNullOrEmpty(source)) return (T)Enum.GetValues(typeof(T)).GetValue(0);
            if(!typeof(T).IsEnum) throw new ArgumentException("T must be an Enum");
            String[] splitflags = source.Split(',');
            var Enumvalues = (from p in splitflags select (T)Enum.Parse(typeof(T), p));
            return ToMask(Enumvalues);

        }
    }
}
