using System;

namespace tfoodies.Libs
{
    public static class StringExtensions
    {
        /// <summary>
        ///     Removes dashes ("-") from the given object value represented as a string and returns an empty string ("")
        ///     when the instance type could not be represented as a string.
        ///     <para>
        ///         Note: This will return the type name of given isntance if the runtime type of the given isntance is not a
        ///         string!
        ///     </para>
        /// </summary>
        /// <param name="value">The object instance to undash when represented as its string value.</param>
        /// <returns></returns>
        public static string UnDash(this object value)
        {
            return ((value as string) ?? string.Empty).UnDash();
        }

        /// <summary>
        ///     Removes dashes ("-") from the given string value.
        /// </summary>
        /// <param name="value">The string value that optionally contains dashes.</param>
        /// <returns></returns>
        public static string UnDash(this string value)
        {
            return (value ?? string.Empty).Replace("-", string.Empty);
        }

        public static string[] ToStrSplit(this string value, int size = 1)
        {
            int count = value.Length / size;

            bool final = false;
            if ((size * count) < value.Length)
            {
                final = true;
            }

            string[] result;
            if (final)
            {
                result = new string[count + 1];
            }
            else
            {
                result = new string[count];
            }

            for (int i = 0; i < count; i++)
            {
                result[i] = value.Substring((i * size), size);
            }

            if (final)
            {
                result[result.Length - 1] = value.Substring(count * size);
            }
            return result;
        }
    }
}
