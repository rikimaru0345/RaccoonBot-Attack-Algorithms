using System;
//test 4
namespace SharedCode
{
    public static class Rand
    {

        private static Random rand = new Random();

        /// <summary>
        /// Create random Float between the specified values.
        /// </summary>
        /// <param name="valueA"></param>
        /// <param name="valueB"></param>
        /// <returns></returns>
        public static float Float(float valueA, float valueB)
        {
            if (valueA > valueB)
            {
                return (float)rand.NextDouble() * (valueA - valueB) + valueB;
            }
            else
            {
                return (float)rand.NextDouble() * (valueB - valueA) + valueA;
            }
        }

        /// <summary>
        /// Create random Integer between the specified values.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int Int(int valueA, int valueB)
        {
            if (valueA > valueB)
            {
                return rand.Next(valueB, valueA);
            }
            else
            {
                return rand.Next(valueA, valueB);
            }
        }
    }
}
