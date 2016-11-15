using CoC_Bot.API;
using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API.Buildings;

namespace SharedCode
{
    public static class HumanLikeAlgorithms
    {
        //Not the exact center of the map, but close. (This is how to create a point using a float value, otherwise, if you create it at 0,0, it uses a different coordinates system, and shows up at the top left corner of the screen.)
        public static PointFT Origin = new PointFT(-0.01f, 0.01f);

        //Constants - Pull from Config File??
        private readonly static float _townHallToRedZoneMinDistance = 5.5f;
        private readonly static float _townHallCenterToOuterEdgeDistance = 2.1f;
        private readonly static float _gruntDeployDistanceFromRedline = 0.5f;
        private readonly static float _rangedDeployDistanceFromRedline = 1.5f;

        /// <summary>
        /// Takes a single Object of whatever type and returns it wrapped in an IEnumerable.
        /// </summary>
        /// <typeparam name="T">The type to return wrapped in IEnumerable</typeparam>
        /// <param name="item">The instance of the object to wrap.</param>
        /// <returns></returns>
        public static IEnumerable<T> ToEnumerable<T>(this T item)
        {
            yield return item;
        }

        /// <summary>
        /// Determines a line from the StartPoint, to the EndPoint. Then extends the line by the specified number of tiles beyond the end point and returns those coordiantes as a PointFT.
        /// </summary>
        /// <param name="startPoint">The Starting Point to begin Drawing the line.</param>
        /// <param name="endPoint">The ending Point where the line is drawn.</param>
        /// <param name="distanceOutInTiles">The length in Tiles to extend the line past the end point.</param>
        /// <returns>PointFT that is on the same line as the start and end points.</returns>
        private static PointFT PointOnLineAwayFrom(this PointFT startPoint, PointFT endPoint, float distanceOutInTiles)
        {
            //calculate a point on the line x1-y1 to x2-y2 that is distance from x2-y2

            float vx = startPoint.X - endPoint.X; // x vector
            float vy = startPoint.Y - endPoint.Y; // y vector

            float mag = (float)Math.Sqrt(vx * vx + vy * vy); // length

            //Normalize to unit length
            vx /= mag;
            vy /= mag;

            //Calculate the point
            float px = (endPoint.X + vx * -distanceOutInTiles);
            float py = (endPoint.Y + vy * -distanceOutInTiles);

            return SafePoint(px, py);  //Point that is {distanceOutInTiles} away from endPoint on the same line as StartPont and endPoint.
        }

        /// <summary>
        /// Returns a set of random points within a square area around the specified point.
        /// </summary>
        /// <param name="originalPoint"></param>
        /// <param name="distanceAroundPointInTiles"></param>
        /// <param name="numberOfPointsToReturn"></param>
        /// <returns></returns>
        public static PointFT[] RandomPointsInArea(this PointFT originalPoint, float distanceAroundPointInTiles, int numberOfPointsToReturn)
        {
            PointFT[] retval = new PointFT[numberOfPointsToReturn];

            //Determine The boundries of the area
            float maxY = originalPoint.Y + (distanceAroundPointInTiles / 2);
            float minY = originalPoint.Y - (distanceAroundPointInTiles / 2);

            float maxX = originalPoint.X + (distanceAroundPointInTiles / 2);
            float minX = originalPoint.X - (distanceAroundPointInTiles / 2);

            for (int i = 0; i < numberOfPointsToReturn; i++)
            {
                PointFT newPoint = SafePoint(Rand.Float(minX, maxX),  Rand.Float(minY, maxY));
                retval[i] = newPoint;
            }

            return retval;
        }

        /// <summary>
        /// Given a Set of Attack Targets this will re-order the Array Starting at the target Index, and then finding the closest neighboring point to add to the output next 
        /// If Targets are arranged in a circle, it will go clockwise, or counter clockwise - depending on which neighbor is closer...
        /// It starts with the two points that are furthest apart, therefore if all collectors are in a line or something. we always start on one end or another.
        /// </summary>
        /// <param name="targets"></param>
        /// <param name="startingTargetIndex"></param>
        /// <returns></returns>
        public static Target[] ReorderToClosestNeighbor(this Target[] targets)
        {
            //First Find the Two points that are furthest from eachother.
            //To do this, we need to compare each point against every other point... 
            //lets do this and store the results so we only have to calculate the distances once.

            if (targets == null || targets.Length == 0)
                return new Target[0]; //An Empty array or null was passed in... Return an Empty Array.

            if (targets.Length < 2)
                return targets; //If there is only 1 or 2 items in the input array, there is no need to Rearrange anything, just return the original array.

            //Set distances between all items in array. (calcualted once)
            Dictionary<KeyValuePair<int, int>, float> distances = new Dictionary<KeyValuePair<int, int>, float>();
            float longestDistance = 0;
            int longestIndexA = -1;
            int longestIndexB = -1;
            int length = targets.Length;

            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < length; j++)
                {
                    if (i >= j) continue; //Dont compare points to themselves, and Skip distances that have already been calculated!

                    float currentDistance = targets[i].Center.DistanceSq(targets[j].Center);
                    if (longestDistance < currentDistance)
                    {
                        longestDistance = currentDistance;
                        longestIndexA = i;
                        longestIndexB = j;
                    }
                    //Always added where i is Less than j due to the way we are skipping comparing things that are already compared.
                    distances.Add(new KeyValuePair<int, int>(i, j), currentDistance);
                }
            }

            //Now we have all distances calculated between all points, and we know which two points are the farthest away from eachother.
            //Choose one end collector (Based on which is closer to the top of the array - doesnt matter how as long as its consistent).
            //TODO - At some point we might say, pick the collector that is closest to an edge...
            int startingTargetIndex;

            if (longestIndexA < longestIndexB)
                startingTargetIndex = longestIndexA;
            else
                startingTargetIndex = longestIndexB;

            //Now go through the array and re-arrange so the points are the clsoest to eachother.
            //This gives more attack strength when overwhelming collectors with about 10 troops on each that are right next to eachother.
            Target[] newTargets = new Target[targets.Length];
            HashSet<int> pointsBlacklist = new HashSet<int>();

            //Start with The End collector.
            newTargets[0] = targets[startingTargetIndex];
            pointsBlacklist.Add(startingTargetIndex);
            var newPointsIndex = 1;
            var previousIndex = startingTargetIndex;
            int smallerIndex;
            int largerIndex;

            while (pointsBlacklist.Count < targets.Length)
            {
                float shortestDistance = float.MaxValue;
                int shortestIndex = -1;

                for (int i = 0; i < targets.Length; i++)
                {
                    if (pointsBlacklist.Contains(i))
                        continue;

                    //Figure out how the index is listed for the distance between these two in the dictionary.
                    if (previousIndex < i)
                    {
                        smallerIndex = previousIndex;
                        largerIndex = i;
                    }
                    else
                    {
                        smallerIndex = i;
                        largerIndex = previousIndex;
                    }

                    //Lookup the distance.
                    float currentDistance = distances[new KeyValuePair<int, int>(smallerIndex, largerIndex)];
                    if (currentDistance < shortestDistance)
                    {
                        shortestIndex = i;
                        shortestDistance = currentDistance;
                    }
                }

                previousIndex = shortestIndex;
                newTargets[newPointsIndex] = targets[shortestIndex];
                pointsBlacklist.Add(shortestIndex);

                newPointsIndex++;
            }

            return newTargets;
        }

        /// <summary>
        /// This function will go through a set of targets that are ordered, and find targets that are close together.
        /// It will then group them into individual Sets of Targets.  (Which will be in the same order, just grouped)
        /// </summary>
        /// <param name="targets">Set of targets ordred with closest pairs together.</param>
        /// <returns></returns>
        public static List<Target[]> GroupCloseTargets(this Target[] targets)
        {

            List<Target[]> groupedTargets = new List<Target[]>();

            //If there are 0 or 1 points in the input array, just return that array wrapped in the list.
            if (targets.Length <= 1)
            {
                groupedTargets.Add(targets);
                return groupedTargets;
            }

            List<Target> newTarget = new List<Target>();
            newTarget.Add(targets[0]); //Always add the 1st point to the First Group.

            //go through the points
            for (int i = 0; i < targets.Length - 1; i++)
            {
                //Check the distance from Center of the Target to the Center of other targets.
                float currentDistance = targets[i].Center.DistanceSq(targets[i + 1].Center);

                Log.Debug($"[Bert's Agorithms] DistanceSq from {targets[i].Name}_{i} point {targets[i + 1].Name}_{i + 1}: {currentDistance}");

                if (currentDistance <= 32f) //Todo Move this into a config setting.
                {
                    //Points are close - should be in the same group.
                    newTarget.Add(targets[i + 1]);
                }
                else
                {
                    //Points are far - should be in seperate groups.
                    groupedTargets.Add(newTarget.ToArray()); //add what is currently in the new set to the output.
                    newTarget = new List<Target>(); //Create a new Group.
                    newTarget.Add(targets[i + 1]); //Add the current point to the new set.
                }
            }

            //At the end of our loop, we will always have at least 1 item in the newPointSet list... Add whatever is in it to the output.
            groupedTargets.Add(newTarget.ToArray()); //add what is currently in the new set to the output.

            Log.Debug($"[Bert's Agorithms] {targets.Length} points split into {groupedTargets.Count} Groups.");

            //Return the Grouped Points.
            return groupedTargets;
        }



        /// <summary>
        /// Filters the List of Deploy Elements, and returns the single type of unit that has the largest count currently.
        /// So if the Deployment element list has Barbarians (Count 20) and Goblins (Count 10) it will return an array with only the Barbs(Count20).
        /// This mimics a human where he would look to see which type of units he has the most of, and then drop those on collectors first... 
        /// </summary>
        /// <param name="deployElements">List of multiple element types. (eg Ground units goblins, barbs), or Ranged units (Archers, Minions)</param>
        /// <returns>Array with the single type of Deploy Element that has the most unit count.</returns>
        public static DeployElement[] FilterTypesByCount(this IEnumerable<DeployElement> deployElements)
        {
            List<DeployElement> filteredList = new List<DeployElement>();

            int unitCount = -1;
            DeployElement most = null;
            foreach (var item in deployElements)
            {
                if (item.Count > unitCount)
                {
                    unitCount = item.Count;
                    most = item;
                }
            }

            if (most != null)
            {
                filteredList.Add(most);
            }

            return filteredList.ToArray();
        }

        /// <summary>
        /// Tests to see whether or not we can easily reach the town hall to snipe it or not.
        /// </summary>
        /// <param name="townHall"></param>
        /// <returns></returns>
        public static bool CanSnipe(this TownHall townHall)
        {
            if (townHall != null)
            {
                Target target = townHall.GetTownHallPoints();

                Log.Debug($"[Berts Agorithms] Town Hall Center Location: X:{target.Center.X} Y:{target.Center.Y}");
                Log.Debug($"[Berts Agorithms] Town Hall Outer Edge Location: X:{target.Edge.X} Y:{target.Edge.Y}");
                Log.Debug($"[Berts Agorithms] DistanceSq from Town Hall to closest outer red point: {target.EdgeToRedline.ToString("F1")}");

                if (target.EdgeToRedline < _townHallToRedZoneMinDistance)  // means there is no wall or building between us and the OUTSIDE of the Town Hall
                {
                    return true;
                }
            }

            return false;
        }

        public static Target GetSnipeDeployPoints(this TownHall townHall)
        {
            Target target = new Target();
            target.ValidTarget = false;

            if (townHall != null)
            {
                target = townHall.GetTownHallPoints();

                if (target.EdgeToRedline == 0)
                {
                    target.DeployGrunts = target.Center.PointOnLineAwayFrom(target.Edge, 0.5f);  //TODO Move to Constants..
                    target.DeployRanged = target.Center.PointOnLineAwayFrom(target.Edge, 2.0f); //TODO Move to Constants..
                }
                else
                {
                    //TODO - should this be some sort of functon to find the closest point that is actually outside of the redZone on the line, and within the bounds of the Map. (Not a Guess)
                    target.DeployGrunts = target.Edge.PointOnLineAwayFrom(target.NearestRedLine, 1.0f); //TODO Move to Constants..
                    target.DeployRanged = target.Edge.PointOnLineAwayFrom(target.NearestRedLine, 2.5f); //TODO Move to Constants..
                }

                Log.Debug($"[Berts Agorithms] Towh Hall Grunt Snipe Point:  X:{target.DeployGrunts.X} Y:{target.DeployGrunts.Y}");
                Log.Debug($"[Berts Agorithms] Towh Hall Ranged Snipe Point:  X:{target.DeployRanged.X} Y:{target.DeployRanged.Y}");

                if (target.EdgeToRedline < _townHallToRedZoneMinDistance)  // means there is no wall or building between us and the OUTSIDE of the Town Hall
                {
                    target.ValidTarget = true;
                }
                else
                {
                    target.ValidTarget = false;
                }
            }
            return target;
        }

        private static Target GetTownHallPoints(this TownHall th)
        {
            Target target = new Target();

            target.Center = th.Location.GetCenter(); //Center of the Town Hall that was found.
            target.Edge = Origin.PointOnLineAwayFrom(target.Center, _townHallCenterToOuterEdgeDistance);

            target.NearestRedLine = GameGrid.RedPoints.OrderBy(p => p.DistanceSq(target.Edge)).First();
            target.EdgeToRedline = target.Edge.DistanceSq(target.NearestRedLine);
            target.TargetBuilding = th;

            return target;
        }

        public static int CountRipeCollectors(float minimumDistance, bool ignoreGold, bool ignoreElixir, CacheBehavior behavior = CacheBehavior.Default)
        {
            Target[] targets = GenerateTargets(minimumDistance, ignoreGold, ignoreElixir, behavior);
            return targets.Length;
        }

        public static Target[] GenerateTargets(float minimumDistance, bool ignoreGold, bool ignoreElixir, CacheBehavior behavior = CacheBehavior.Default)
        {
            // Find all Collectors & storages just sitting around...
            List<Building> buildings = new List<Building>();

            if (!ignoreGold)
            {
                //User has Gold min set to ZERO - which means Dont include Gold Targets
                buildings.AddRange(GoldMine.Find(behavior));
                buildings.AddRange(GoldStorage.Find(behavior));
            }

            if (!ignoreElixir)
            {
                //User has Elixir min set to ZERO - which means Dont include Elixir Targets
                buildings.AddRange(ElixirCollector.Find(behavior));
                buildings.AddRange(ElixirStorage.Find(behavior));
            }

            //We always includ DarkElixir - Because who doesnt love dark Elixir?
            buildings.AddRange(DarkElixirDrill.Find(behavior));
            buildings.AddRange(DarkElixirStorage.Find(behavior));

            List<Target> targetList = new List<Target>();

            foreach (Building building in buildings)
            {
                Target current = new Target();

                current.TargetBuilding = building;
                current.Center = building.Location.GetCenter();
                current.NearestRedLine = GameGrid.RedPoints.OrderBy(p => p.DistanceSq(current.Center)).First();
                current.CenterToRedline = current.Center.DistanceSq(current.NearestRedLine);
                Log.Debug($"[Berts Algorithms] DistanceSq from {current.Name} to red point: {current.CenterToRedline.ToString("F1")}");
                if (current.CenterToRedline < minimumDistance)  //Compare distance to Redline to the Minimum acceptable distance Passed in
                {
                    current.DeployGrunts = current.Center.PointOnLineAwayFrom(current.NearestRedLine, _gruntDeployDistanceFromRedline); //Barbs & Goblins
                    current.DeployRanged = current.Center.PointOnLineAwayFrom(current.NearestRedLine, _rangedDeployDistanceFromRedline); //Archers & Minions

                    targetList.Add(current);
                }
            }

            Log.Debug($"[Berts Algorithms] Found {targetList.Count} deploy points");

            return targetList.ToArray();
        }
        public static PointFT SafePoint(float x, float y) {
            //Make sure the point is inside the Playing field
            if (x > GameGrid.MaxX + 2) x = GameGrid.MaxX + 1.8f;
            if (x < GameGrid.MinX - 2) x = GameGrid.MinX - 1.8f;
            if (y > GameGrid.MaxY + 2) y = GameGrid.MaxY + 1.8f;
            if (y < GameGrid.MinY - 2) y = GameGrid.MinY - 1.8f;

            return new PointFT(x, y);
        }
    }
}
