using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Seq.App.Azure.DevOps
{
    /// <summary>
    /// Provides a centralized place for common functionality exposed via extension methods.
    /// </summary>
    public static partial class ExtensionMethods
    {
        public static ConcurrentBag<KeyValuePair<string,string>> ParseKeyValueArray(this string valueString)
        {
            ConcurrentBag<KeyValuePair<string, string>> values = new ConcurrentBag<KeyValuePair<string, string>>();

            if (!string.IsNullOrEmpty(valueString))
            {
                foreach(var val in valueString.Split(',').ToArray())
                {
                    string[] temp = val.Split(':').ToArray();
                    if (temp.GetUpperBound(0)> 1)
                    {
                        values.Add(new KeyValuePair<string,string>(temp[0], temp[1]));
                    }
                }
            }

            return values;
        }

        /// <summary>
        /// Answers true if this String is either null or empty.
        /// </summary>
        /// <remarks>I'm so tired of typing String.IsNullOrEmpty(s)</remarks>
        public static bool IsNullOrEmpty(this string s) => string.IsNullOrEmpty(s);

        /// <summary>
        /// Answers true if this String is neither null or empty.
        /// </summary>
        /// <remarks>I'm also tired of typing !String.IsNullOrEmpty(s)</remarks>
        public static bool HasValue(this string s) => !string.IsNullOrEmpty(s);

        /// <summary>
        /// force string to be maxlen or smaller
        /// </summary>
        public static string Truncate(this string s, int maxLength) =>
            s.IsNullOrEmpty() ? s : (s.Length > maxLength ? s.Remove(maxLength) : s);

        public static string TruncateWithEllipsis(this string s, int maxLength) =>
            s.IsNullOrEmpty() || s.Length <= maxLength ? s : Truncate(s, Math.Max(maxLength, 3) - 3) + "…";
    }
}
