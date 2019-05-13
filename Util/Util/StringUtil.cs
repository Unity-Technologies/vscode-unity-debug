using System.Collections.Generic;
using System.Text;

namespace Util.Concurrency
{
    public static class StringUtil
    {
        public static string Join(this IEnumerable<string> strings, string separator)
        {
            if (strings is IList<string> stringList)
            {
                int count = stringList.Count;
                if (count == 0)
                    return string.Empty;
                int num = 0;
                for (int index = 0; index < count; ++index)
                {
                    if (stringList[index] != null)
                        num += stringList[index].Length;
                }
                StringBuilder stringBuilder = new StringBuilder(num + separator.Length * (count - 1));
                for (int index = 0; index < count; ++index)
                {
                    if (index != 0)
                        stringBuilder.Append(separator);
                    stringBuilder.Append(stringList[index]);
                }
                return stringBuilder.ToString();
            }
            StringBuilder stringBuilder1 = new StringBuilder();
            bool flag = false;
            foreach (string str in strings)
            {
                if (flag)
                    stringBuilder1.Append(separator);
                else
                    flag = true;
                stringBuilder1.Append(str);
            }
            return stringBuilder1.ToString();
        }
    }
}