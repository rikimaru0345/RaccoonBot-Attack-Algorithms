using System;
using System.Linq;
using System.Windows;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Internals;

namespace SmartAirDeploy
{
    static class SmartAirDeployHelpers
    {
        /// <summary>
        /// Returns true if we are certain that we found all AirDefenses, otherwise false
        /// </summary>
        /// <param name="townHallLevel">The opponent's townhall level</param>
        /// <returns>True: We found all air defenses, False: We don't know if we found all Air Defenses</returns>
        internal static bool AllAirDefensesFound(int? townHallLevel)
        {
            var foundAirDefensesCount = AirDefense.Find().Count();
            var maxAirDefensesCount = CountMaxPossibleAirDefenses(townHallLevel);
            if (foundAirDefensesCount >= maxAirDefensesCount) return true;

            Logger.Info($"[Smart Air] Couldn't find all AirDefenses. Found {foundAirDefensesCount}/{maxAirDefensesCount} AirDefenses.");
            return false;
        }

        /// <summary>
        /// Returns the amount of maximum possible Air Defenses for the given TownHallLevel
        /// </summary>
        /// <param name="townHallLevel">The opponent's Townhall level</param>
        /// <returns>The quantity of maximum possible Air Defenses</returns>
        internal static int CountMaxPossibleAirDefenses(int? townHallLevel)
        {
            var buildingName = typeof(AirDefense).Name;

            int thLevel;
            // If TownhallLevel couldn't be found set it to the highest TownhallLevel possible
            if (!townHallLevel.HasValue) // wenn kein wert hat, also der innere wert null ist
            {
                thLevel = TownHallLimits.Limits.Length - 1;
                Log.Info($"[Smart Air] Couldn't find the Townhall, going to assume the opponent is townhall level {thLevel}");
            }
            else
                thLevel = townHallLevel.Value;

            TownHallLimit limit;
            TownHallLimits.Limits[thLevel].TryGetValue(buildingName, out limit);

            return limit.Quantity;
        }
        
        /// <summary>
        /// Calculates the squared distance to the closest possible deploypoint
        /// </summary>
        /// <param name="sourceCenter">The point which should be used for the distance calculation</param>
        /// <returns>The squared distance from the sourcePoint to the nearest deploypoint</returns>
        internal static float DistanceSqToClosestDeploypoint(PointFT sourceCenter)
        {
            return GameGrid.RedPoints.Min(point => point.DistanceSq(sourceCenter));
        }

        const double Epsilon = 1e-10;
        public static bool IsZero(this double d)
        {
            return Math.Abs(d) < Epsilon;
        }

        public static double Cross(this Vector t, Vector v)
        {
            return t.X * v.Y - t.Y * v.X;
        }

        /// <summary>
        /// Checks if there is an intersection and calculates the intersection point as vector
        /// </summary>
        /// <param name="p">Line p , Vectorpoint 1</param>
        /// <param name="p2">Line p , Vectorpoint 2</param>
        /// <param name="q">Line q , Vectorpoint 1</param>
        /// <param name="q2">Line q , Vectorpoint 2</param>
        /// <param name="intersection">Intersectionpoint as Vector</param>
        /// <param name="considerCollinearOverlapAsIntersect"></param>
        /// <returns></returns>
        public static bool LineSegementsIntersect(Vector p, Vector p2, Vector q, Vector q2, out Vector intersection, bool considerCollinearOverlapAsIntersect = false)
        {
            intersection = new Vector();

            var r = p2 - p;
            var s = q2 - q;
            var rxs = r.Cross(s);
            var qpxr = (q - p).Cross(r);

            // If r x s = 0 and (q - p) x r = 0, then the two lines are collinear.
            if (rxs.IsZero() && qpxr.IsZero())
            {
                // 1. If either  0 <= (q - p) * r <= r * r or 0 <= (p - q) * s <= * s
                // then the two lines are overlapping,
                if (considerCollinearOverlapAsIntersect)
                    if ((0 <= (q - p) * r && (q - p) * r <= r * r) || (0 <= (p - q) * s && (p - q) * s <= s * s))
                        return true;

                // 2. If neither 0 <= (q - p) * r = r * r nor 0 <= (p - q) * s <= s * s
                // then the two lines are collinear but disjoint.
                // No need to implement this expression, as it follows from the expression above.
                return false;
            }

            // 3. If r x s = 0 and (q - p) x r != 0, then the two lines are parallel and non-intersecting.
            if (rxs.IsZero() && !qpxr.IsZero())
                return false;

            // t = (q - p) x s / (r x s)
            var t = (q - p).Cross(s) / rxs;

            // u = (q - p) x r / (r x s)

            var u = (q - p).Cross(r) / rxs;

            // 4. If r x s != 0 and 0 <= t <= 1 and 0 <= u <= 1
            // the two line segments meet at the point p + t r = q + u s.
            if (!rxs.IsZero() && (0 <= t && t <= 1) && (0 <= u && u <= 1))
            {
                // We can calculate the intersection point using either t or u.
                intersection = p + t * r;

                // An intersection was found.
                return true;
            }

            // 5. Otherwise, the two line segments are not parallel but do not intersect.
            return false;
        }

    }
}