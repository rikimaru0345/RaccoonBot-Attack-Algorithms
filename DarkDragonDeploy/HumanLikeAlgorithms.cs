using CoC_Bot.API;
using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot.API.Buildings;
using System.Drawing;

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
        private readonly static float _DEStorageCenterToOuterEdgeDistance = 1f;

        private static PointFT[] GreenPoints = GenerateGreenPoints();

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
        public static PointFT PointOnLineAwayFromEnd(this PointFT startPoint, PointFT endPoint, float distanceOutInTiles)
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
        /// Determines a line from the StartPoint, to the EndPoint. Then extends the line by the specified number of tiles beyond the end point and returns those coordiantes as a PointFT.
        /// </summary>
        /// <param name="startPoint">The Starting Point to begin Drawing the line.</param>
        /// <param name="endPoint">The ending Point where the line is drawn.</param>
        /// <param name="distanceOutInTiles">The length in Tiles to extend the line past the end point.</param>
        /// <returns>PointFT that is on the same line as the start and end points.</returns>
        public static PointFT PointOnLineAwayFromStart(this PointFT startPoint, PointFT endPoint, float distanceOutInTiles)
        {
            //calculate a point on the line x1-y1 to x2-y2 that is distance from x2-y2

            float vx = startPoint.X - endPoint.X; // x vector
            float vy = startPoint.Y - endPoint.Y; // y vector

            float mag = (float)Math.Sqrt(vx * vx + vy * vy); // length

            //Normalize to unit length
            vx /= mag;
            vy /= mag;

            //Calculate the point
            float px = (startPoint.X + vx * -distanceOutInTiles);
            float py = (startPoint.Y + vy * -distanceOutInTiles);

            return SafePoint(px, py);  //Point that is {distanceOutInTiles} away from startPoint on the same line as StartPont and endPoint.
        }


        /// <summary>
        /// Checks to see if the item is null, and if it has any troops left to deploy, if so, it adds it to the List.
        /// </summary>
        /// <param name="remainingElements">List to add the items to</param>
        /// <param name="item">Deployment Element to add if it is not used up.</param>
        public static void RecountAndAddIfAny(this List<DeployElement> remainingElements, DeployElement item)
        {
            if (remainingElements == null)
                remainingElements = new List<DeployElement>();

            if (item == null) return;

            item.Recount();

            if (item.Count > 0)
            {
                remainingElements.Add(item);
            }
        }

        /// <summary>
        /// Checks to see if each item in the list is null, and if it has any troops left to deploy, if so, it adds it to the List.
        /// </summary>
        /// <param name="remainingElements">List to add the items to</param>
        /// <param name="item">Deployment Element to add if it is not used up.</param>
        public static void RecountAndAddIfAny(this List<DeployElement> remainingElements, List<DeployElement> items)
        {
            if (remainingElements == null)
                remainingElements = new List<DeployElement>();

            foreach (var item in items)
            {
                if (item == null) return;

                item.Recount();

                if (item.Count > 0)
                {
                    remainingElements.Add(item);
                }
            }
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
                PointFT newPoint = SafePoint(Rand.Float(minX, maxX), Rand.Float(minY, maxY));
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
        /// Returns the total Unit count across all different Unit types in the list.
        /// </summary>
        /// <param name="deployElements"></param>
        /// <returns></returns>
        public static int TotalUnitCount(this IEnumerable<DeployElement> deployElements)
        {
            var totalCount = 0;
            foreach (var item in deployElements)
            {
                totalCount += item.Count;
            }
            return totalCount;
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

        public static Target GetSnipeDeployPoints(this TownHall townHall)
        {
            Target target = new Target();
            target.ValidTarget = false;

            if (townHall != null)
            {
                target = townHall.GetTownHallPoints();

                if (target.EdgeToRedline == 0)
                {
                    target.DeployGrunts = target.Center.PointOnLineAwayFromEnd(target.Edge, 0.5f);  //TODO Move to Constants..
                    target.DeployRanged = target.Center.PointOnLineAwayFromEnd(target.Edge, 2.0f); //TODO Move to Constants..
                }
                else
                {
                    //TODO - should this be some sort of functon to find the closest point that is actually outside of the redZone on the line, and within the bounds of the Map. (Not a Guess)
                    target.DeployGrunts = target.Edge.PointOnLineAwayFromEnd(target.NearestRedLine, 1.0f); //TODO Move to Constants..
                    target.DeployRanged = target.Edge.PointOnLineAwayFromEnd(target.NearestRedLine, 2.5f); //TODO Move to Constants..
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
            target.Edge = Origin.PointOnLineAwayFromEnd(target.Center, _townHallCenterToOuterEdgeDistance);

            target.NearestRedLine = AllPoints.OrderBy(p => p.DistanceSq(target.Edge)).First();
            target.EdgeToRedline = target.Edge.DistanceSq(target.NearestRedLine);
            target.TargetBuilding = th;

            return target;
        }

        public static int CountRipeCollectors(string algorithmName, float minimumDistance, bool ignoreGold, bool ignoreElixir, out double fillState, out double collectorLvl, CacheBehavior behavior = CacheBehavior.Default, bool activeBase = false)
        {
            Target[] targets = GenerateTargets(algorithmName, minimumDistance, ignoreGold, ignoreElixir, out fillState, out collectorLvl, behavior, false, activeBase);
            return targets.Length;
        }

        public static Target[] GenerateTargets(string algorithmName, float minimumDistance, bool ignoreGold, bool ignoreElixir, out double avgFillstate, out double avgCollectorLvl, CacheBehavior behavior = CacheBehavior.Default, bool outputDebugImage = false, bool activeBase = false)
        {
            // Find all Collectors & storages just sitting around...
            List<Building> buildings = new List<Building>();

            //Get a list of Gold Mines.
            List<GoldMine> goldMines = new List<GoldMine>();
            goldMines.AddRange(GoldMine.Find(behavior));

            //Get a list of Elixir Collectors.
            List<ElixirCollector> elixirCollectors = new List<ElixirCollector>();
            elixirCollectors.AddRange(ElixirCollector.Find(behavior));
            avgFillstate = 0;

            //Get the Average Fill State of all the Elixir Collectors - From this we can tell what percentage of the loot is in Collectors.
            if (elixirCollectors.Count > 1)
            {
                avgFillstate = elixirCollectors.Average(c => c.FillState);
            }

            //Log the Average Fill State of aLL elixir Collectors...
            Log.Debug($"[Berts Algorithms] - Fill State Average of ALL Elixir Collectors: {(avgFillstate * 10).ToString("F1")}");

            if (!ignoreGold)
            {
                buildings.AddRange(goldMines);
                if (activeBase)
                    buildings.AddRange(GoldStorage.Find(behavior));
            }
            if (!ignoreElixir)
            {
                buildings.AddRange(elixirCollectors);
                if (activeBase)
                    buildings.AddRange(ElixirStorage.Find(behavior));
            }

            //Determine the Average Collector Level.
            avgCollectorLvl = 0;

            if (ignoreGold && !ignoreElixir)
            {
                if (elixirCollectors.Count(c => c.Level.HasValue) > 1)
                {
                    avgCollectorLvl = elixirCollectors.Where(c => c.Level.HasValue).Average(c => (int)c.Level);
                }
            }
            else if (ignoreElixir && !ignoreGold)
            {
                if (goldMines.Count(c => c.Level.HasValue) > 1)
                {
                    avgCollectorLvl = goldMines.Where(c => c.Level.HasValue).Average(c => (int)c.Level);
                }
            }
            else if (!ignoreElixir && !ignoreGold)
            {
                if (buildings.Count(c => c.Level.HasValue) > 1)
                {
                    avgCollectorLvl = buildings.Where(c => c.Level.HasValue).Average(c => (int)c.Level);
                }
            }

            //We always includ DarkElixir - Because who doesnt love dark Elixir?
            buildings.AddRange(DarkElixirDrill.Find(behavior));
            if (activeBase)
                buildings.AddRange(DarkElixirStorage.Find(behavior));

            List<Target> targetList = new List<Target>();

            foreach (Building building in buildings)
            {
                Target current = new Target();

                current.TargetBuilding = building;
                current.Center = building.Location.GetCenter();
                current.Edge = Origin.PointOnLineAwayFromEnd(current.Center, 1.0f);
                current.NearestRedLine = AllPoints.OrderBy(p => p.DistanceSq(current.Edge)).First();
                current.CenterToRedline = current.Center.DistanceSq(current.NearestRedLine);
                if (current.CenterToRedline < minimumDistance)  //Compare distance to Redline to the Minimum acceptable distance Passed in
                {
                    Log.Debug($"[Berts Algorithms] Distance from {current.Name} to red point: {Math.Sqrt(current.CenterToRedline).ToString("F1")}, Min Distance: {Math.Sqrt(minimumDistance).ToString("F1")} - GO!");
                    current.DeployGrunts = current.Center.PointOnLineAwayFromEnd(current.NearestRedLine, _gruntDeployDistanceFromRedline); //Barbs & Goblins
                    current.DeployRanged = current.Center.PointOnLineAwayFromEnd(current.NearestRedLine, _rangedDeployDistanceFromRedline); //Archers & Minions

                    targetList.Add(current);
                }
                else
                {
                    Log.Debug($"[Berts Algorithms] Distance from {current.Name} to red point: {Math.Sqrt(current.CenterToRedline).ToString("F1")}, Min Distance: {Math.Sqrt(minimumDistance).ToString("F1")} - TOO FAR!");
                }
            }

            if (outputDebugImage)
            {
                OutputDebugImage(algorithmName, buildings, targetList);
            }

            return targetList.ToArray();
        }

        private static void OutputDebugImage(string algorithmName, List<Building> buildings, List<Target> targetList)
        {

            var d = DateTime.UtcNow;
            var debugFileName = $"{algorithmName} {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}";
            //Get a screen Capture of all targets we found...
            using (Bitmap canvas = Screenshot.Capture())
            {

                Screenshot.Save(canvas, $"{debugFileName}_1");

                Visualize.Axes(canvas);
                Visualize.Grid(canvas, redZone: true);

                //Draw the Max Outside Boundry For Deployment...
                for (int i = -35; i <= 35; i++)
                {
                    var color = Color.Magenta;
                    if (i % 2 == 0)
                        color = Color.LightPink;

                    float max = 30f;
                    DrawLine(canvas, color, SafePoint(max, i), SafePoint(max, i + 1)); //Top Left Side
                    DrawLine(canvas, color, SafePoint(i, max), SafePoint(i + 1, max)); //Top Right Side
                    DrawLine(canvas, color, SafePoint(i + 1, -max), SafePoint(i, -max)); //Bottom Left Side
                    DrawLine(canvas, color, SafePoint(-max, i + 1), SafePoint(-max, i)); //Bottom Right Side
                }

                //Temporary Draw all the Redpoints.
                foreach (var point in GameGrid.RedPoints)
                {
                    DrawPoint(canvas, Color.Red, point);
                }

                //Temporary Draw all the Greenpoints.
                foreach (var point in GreenPoints)
                {
                    DrawPoint(canvas, Color.Green, point);
                }

                foreach (var building in buildings)
                {
                    var color = Color.White;
                    if (building.GetType() == typeof(ElixirCollector) || building.GetType() == typeof(ElixirStorage))
                    {
                        color = Color.Violet;
                    }
                    if (building.GetType() == typeof(GoldMine) || building.GetType() == typeof(GoldStorage))
                    {
                        color = Color.Gold;
                    }
                    if (building.GetType() == typeof(DarkElixirDrill) || building.GetType() == typeof(DarkElixirStorage))
                    {
                        color = Color.Brown;
                    }

                    //Draw a target on each building.
                    Visualize.Target(canvas, building.Location.GetCenter(), 40, color);

                }
                //Save the Image to the Debug Folder...
                Screenshot.Save(canvas, $"{debugFileName}_2");
            }

            //Get a screen Capture of all targets we found...
            using (Bitmap canvas = Screenshot.Capture())
            {
                foreach (var target in targetList)
                {
                    var color = Color.White;
                    if (target.TargetBuilding.GetType() == typeof(ElixirCollector) || target.TargetBuilding.GetType() == typeof(ElixirStorage))
                    {
                        color = Color.Violet;
                    }
                    if (target.TargetBuilding.GetType() == typeof(GoldMine) || target.TargetBuilding.GetType() == typeof(GoldStorage))
                    {
                        color = Color.Gold;
                    }
                    if (target.TargetBuilding.GetType() == typeof(DarkElixirDrill) || target.TargetBuilding.GetType() == typeof(DarkElixirStorage))
                    {
                        color = Color.Brown;
                    }

                    //Draw a target on each building.
                    Visualize.Target(canvas, target.TargetBuilding.Location.GetCenter(), 40, color);
                    Visualize.Target(canvas, target.DeployGrunts, 20, color);
                    Visualize.Target(canvas, target.DeployRanged, 20, color);

                }
                //Save the Image to the Debug Folder...
                Screenshot.Save(canvas, $"{debugFileName}_3");
            }

            Log.Debug("[Berts Algorithms] Collector/Storage & Target Debug Images Saved!");
        }

        public static void SaveBasicDebugScreenShot(string algorithmName, string filenameSuffix)
        {
            var d = DateTime.UtcNow;
            var debugFileName = $"{algorithmName} {d.Year}-{d.Month}-{d.Day} {d.Hour}-{d.Minute}-{d.Second}-{d.Millisecond}";
            //Get a screen Capture of all targets we found...
            using (Bitmap canvas = Screenshot.Capture())
            {
                Screenshot.Save(canvas, $"{debugFileName}_{filenameSuffix}");
            }
        }


        public static Target TargetDarkElixirStorage(CacheBehavior behavior = CacheBehavior.Default)
        {
            var target = new Target();

            var des = DarkElixirStorage.Find(behavior);
            target.ValidTarget = false;

            if (des.Length > 0)
            {
                target.ValidTarget = true;
                target.TargetBuilding = des[0];
                target.Center = des[0].Location.GetCenter();

                target.Edge = Origin.PointOnLineAwayFromEnd(target.Center, _DEStorageCenterToOuterEdgeDistance);
                target.NearestRedLine = AllPoints.OrderBy(p => p.DistanceSq(target.Edge)).First();
                target.EdgeToRedline = target.Edge.DistanceSq(target.NearestRedLine);

                //Fill the DeployGrunts Property with where out main dragon force should go.
                target.DeployGrunts = Origin.PointOnLineAwayFromEnd(target.NearestRedLine, 0.2f); //TODO Move to Constants..
            }

            return target;
        }

        public static PointFT[] GetFunnelingPoints(this Target mainGroupDeployPoint, double angleOfFunnel)
        {

            //Get the Distance From the Origin to the Center - main deploy point.
            var distance = Math.Sqrt((Math.Pow(mainGroupDeployPoint.DeployGrunts.X - 0, 2) + Math.Pow(mainGroupDeployPoint.DeployGrunts.Y - 0, 2)));

            Log.Debug($"[Berts Algorithms] Distance {distance.ToString("F1")}");

            Log.Debug($"[Berts Algorithms] Main     ({mainGroupDeployPoint.DeployGrunts.X.ToString("F1")},{mainGroupDeployPoint.DeployGrunts.Y.ToString("F1")})");

            //Determine the angle of the main deploy point from the X-axis.
            double ang1 = Math.Atan(mainGroupDeployPoint.DeployGrunts.Y / mainGroupDeployPoint.DeployGrunts.X);

            //Determine the Angles of the funnel points from the X-axis, by adding/subtracting half of the desired angle of the funnel.
            var ang2 = ang1 + (angleOfFunnel / 2);
            var ang3 = ang1 - (angleOfFunnel / 2);

            Log.Debug($"[Berts Algorithms] Funneling Points - Angles from X:{mainGroupDeployPoint.DeployGrunts.X} Main:{ang1} Funnel1:{ang2} Funnel2:{ang3}");

            //Determine the Funnel Points
            PointFT funnel1;
            PointFT funnel2;
            if (mainGroupDeployPoint.DeployGrunts.X > 0)
            {
                funnel1 = SafePoint(-(float)(distance * Math.Cos(ang2)), -(float)(distance * Math.Sin(ang2)));
                funnel2 = SafePoint(-(float)(distance * Math.Cos(ang3)), -(float)(distance * Math.Sin(ang3)));
            }
            else
            {
                funnel1 = SafePoint((float)(distance * Math.Cos(ang2)), (float)(distance * Math.Sin(ang2)));
                funnel2 = SafePoint((float)(distance * Math.Cos(ang3)), (float)(distance * Math.Sin(ang3)));
            }

            Log.Debug($"[Berts Algorithms] Point1   ({funnel1.X.ToString("F1")},{funnel1.Y.ToString("F1")})");
            Log.Debug($"[Berts Algorithms] Point2   ({funnel2.X.ToString("F1")},{funnel2.Y.ToString("F1")})");

            //Find the closest Redline points to these two funnel points.
            var red1 = AllPoints.OrderBy(p => p.DistanceSq(funnel1)).First();
            var red2 = AllPoints.OrderBy(p => p.DistanceSq(funnel2)).First();

            //Return the two points
            List<PointFT> points = new List<PointFT>();

            points.Add(red1);
            points.Add(red2);

            return points.ToArray();
        }

        /// <summary>
        /// Returns the distance away from the origin this building is.
        /// </summary>
        /// <param name="building"></param>
        /// <returns></returns>
        public static double DistanceFromOrigin(this Building building)
        {
            return DistanceFromPoint(building, 0, 0);
        }

        /// <summary>
        /// Returns the distance in tiles from the center of a building to a specific X,Y Coordiante Point.
        /// </summary>
        /// <param name="building"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static double DistanceFromPoint(this Building building, PointFT point)
        {
            return DistanceFromPoint(building, point.X, point.Y);
        }

        /// <summary>
        /// Returns the distance in tiles from the center of a building to a specific X,Y Coordiante Point.
        /// </summary>
        /// <param name="building"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static double DistanceFromPoint(this Building building, float x, float y)
        {
            return Math.Sqrt((Math.Pow(building.Location.X - x, 2) + Math.Pow(building.Location.Y - y, 2)));
        }


        private static bool IsPointOutsideRedline(this PointFT start)
        {
            var nearestRedLine = AllPoints.OrderBy(p => p.DistanceSq(start)).First();
            var distance = start.DistanceSq(nearestRedLine);

            if (distance == 0)
                return true;
            else
                return false;
        }

        public static PointFT SafePoint(float x, float y)
        {
            //Make sure the point is close to being inside the Playing field
            if (x > GameGrid.MaxX + 8.0f) x = GameGrid.MaxX + 8.0f;
            if (x < GameGrid.MinX - 8.0f) x = GameGrid.MinX - 8.0f;
            if (y > GameGrid.MaxY + 8.0f) y = GameGrid.MaxY + 8.0f;
            if (y < GameGrid.MinY - 8.0f) y = GameGrid.MinY - 8.0f;

            return new PointFT(x, y);
        }

        public static PointFT FindClosestDeployPointOnLine(PointFT origin, PointFT start)
        {

            PointFT result = new PointFT();

            float counter = .5f;

            while (counter < 50)
            {
                var temp = origin.PointOnLineAwayFromEnd(start, counter);
                if (result.IsPointOutsideRedline())
                {
                    result = temp;
                    break;
                }

                counter = counter + .5f;
            }

            return result;
        }

        public static PointFT[] GenerateGreenPoints()
        {
            var points = new HashSet<PointFT>();

            for (int i = -25; i <= 25; i++)
            {
                float max = 25.5f;
                if (i >= -20) //Skip Points off the Right Hand side of Screen.
                    points.Add(new PointFT(max, i)); //Top Right

                points.Add(new PointFT(i, max)); //Top Left

                if (i >= -14) //Skip points that are on the bottom cuttoff.
                    points.Add(new PointFT(-max, i)); //Bottom Left

                if (i >= -13 && i <= 19) //Skip points that are on the bottom cuttoff. & Righthand side of screen.
                    points.Add(new PointFT(i, -(max + 1))); //Bottom Right //Make bottom right more outside Because of incorrect Y shift in the bot.
            }
            return points.ToArray();
        }

        /// <summary>
        /// Combines all RedPoints and GreenPoints arrays into one array.
        /// </summary>
        public static PointFT[] AllPoints
        {
            get
            {
                return GreenPoints.Concat(GameGrid.RedPoints).ToArray();
            }
        }


        /// <summary>
        /// Draws a line between two points on the bitmap
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="color"></param>
        /// <param name="startPoint"></param>
        /// <param name="endPoint"></param>
        public static void DrawLine(Bitmap canvas, Color color, PointFT startPoint, PointFT endPoint)
        {
            using (var g = Graphics.FromImage(canvas))
            {
                var pen = new Pen(color, 2);
                g.DrawLine(pen, startPoint.ToScreenAbsolute().X, startPoint.ToScreenAbsolute().Y, endPoint.ToScreenAbsolute().X, endPoint.ToScreenAbsolute().Y);
            }
        }

        /// <summary>
        /// Draws a Point on the canvas
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="color"></param>
        /// <param name="point"></param>
        public static void DrawPoint(Bitmap canvas, Color color, PointFT point)
        {
            using (var g = Graphics.FromImage(canvas))
            {
                var pen = new Pen(color, 2);
                g.DrawEllipse(pen, point.ToScreenAbsolute().X, point.ToScreenAbsolute().Y, 3, 3);
            }
        }


    }
}
