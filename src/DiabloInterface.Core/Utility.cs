using System;
namespace Zutatensuppe.DiabloInterface.Core
{
    public class Utility
    {
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
                return min;

            if (value.CompareTo(max) > 0)
                return max;

            return value;
        }

        public static int GetUnixTimestamp()
        {
            return (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
