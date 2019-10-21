using System;
using System.Text;

namespace Twino.Core
{
    /// <summary>
    /// Helper class for HTML Encoding operations
    /// </summary>
    public static class HtmlEncoder
    {
        /// <summary>
        /// Encodes expression for HTML encoding
        /// </summary>
        public static string HtmlEncode(string expression, Encoding encode)
        {
            const string exceptions = "1234567890qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM-_";
            var result = new StringBuilder();

            foreach (var c in expression)
            {
                if (exceptions.Contains(c))
                {
                    result.Append(c);
                    continue;
                }

                var hex = BitConverter.ToString(encode.GetBytes(new[] {c}));
                result.Append("%" + hex.Replace("-", "%"));
            }

            return result.ToString();
        }

        /// <summary>
        /// Decodes HTML encoded strings
        /// </summary>
        public static string HtmlDecode(string expression, Encoding encode)
        {
            var result = new StringBuilder();

            var elen = expression.Length;
            for (var i = 0; i < elen; i++)
            {
                if (expression[i] == '%' && i + 2 < elen)
                {
                    if (i + 5 < elen)
                    {
                        if (expression[i + 3] == '%')
                        {
                            var n1 = expression.Substring(i + 1, 2);
                            var n2 = expression.Substring(i + 4, 2);
                            result.Append(encode.GetString(new[] {Convert.ToByte(n1, 16), Convert.ToByte(n2, 16)}));
                            i += 5;
                            continue;
                        }
                    }

                    var num = expression.Substring(i + 1, 2);
                    result.Append(encode.GetString(new[] {Convert.ToByte(num, 16)}));
                    i += 2;
                }
                else
                    result.Append(expression[i]);
            }

            return result.ToString();
        }
    }
}