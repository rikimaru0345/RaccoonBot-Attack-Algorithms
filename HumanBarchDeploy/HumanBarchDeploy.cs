using System;
using System.Collections.Generic;
using System.Linq;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using SharedCode;
using System.Reflection;

[assembly: Addon("HumanBarchDeploy Addon", "Contains the Human Barch deploy algorithm", "Bert")]

namespace HumanBarchDeploy
{
    [AttackAlgorithm("HumanBarchDeploy", "Deploys Barch units close to collectors in a believeable Human pattern.  (So that a review of the attack does not look like a BOT)")]
    class HumanBarchDeploy : BaseAttack
    {
        private float _minimumDistanceToCollectors = 9f;
        private float _minimumAttackDistanceToCollectors = 18f; //Use a slightly different number once we are already attacking... - Get collectors behind other buildings also...
        private int _minimumExposedTargets = 6;
        private float _thDeployRadius = 1.2f;
        private float _collectorDeployRadius = 1.4f;

        private bool IgnoreGold { get; set; }
        private bool IgnoreElixir { get; set; }

        public HumanBarchDeploy(Opponent opponent) : base(opponent) { }

        public override string ToString()
        {
            return "Human Barch Deploy";
        }

        public override double ShouldAccept()
        {
            int returnVal = 0;

            // check if the base meets ALL the user's requirements
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
            {
                return 0;
            }

            //Check if the base is dead.
            if (Opponent.IsDead(true))
            {

                //Check to see if the settings are favoring Gold or Elixir.. (if a resource is set to ZERO, Ignore that resource when searching for targets)
                if (!UserSettings.DeadSearch.NeedsOnlyOneRequirementForAttack)
                {
                    IgnoreGold = (UserSettings.DeadSearch.MinGold == 0);
                    IgnoreElixir = (UserSettings.DeadSearch.MinElixir == 0);
                }

                var minTargets = _minimumExposedTargets;

                //If ignoring Gold, Reduce the min required targets by half.
                if (IgnoreGold)
                {
                    Log.Info($"[Human Barch] Minimum Gold = 0  - Ignoring Gold Storages/Collectors");
                    minTargets = (int)Math.Floor(Convert.ToDouble(minTargets) / 2d);
                }

                //If ignoring Gold, Reduce the min required targets by half.
                if (IgnoreElixir)
                {
                    Log.Info($"[Human Barch] Minimum Elixir = 0  - Ignoring Elixir Storages/Collectors");
                    minTargets = (int)Math.Floor(Convert.ToDouble(minTargets) / 2d); ;
                }
                if (minTargets == 0)
                    minTargets = 1; //if Gold AND Elixir are Ignored, there should be at least 1 target (DE) in order to attack.

                //Check how many Collectors are Ripe for the taking (outside walls)
                int ripeCollectors = HumanLikeAlgorithms.CountRipeCollectors(_minimumDistanceToCollectors, IgnoreGold, IgnoreElixir);

                Log.Info($"[Human Barch] {ripeCollectors} targets found outside walls. Min={minTargets}");

                if (ripeCollectors < minTargets)
                {
                    Log.Info($"[Human Barch] Skipping - {ripeCollectors} targets were found outside the wall. Min={minTargets}");
                    returnVal = 0;
                }
                else
                {
                    return 1;
                }
            }
            else
            {
                //Check to see if the settings are favoring Gold or Elixir.. (if a resource is set to ZERO, Ignore that resource when searching for targets)
                if (!UserSettings.ActiveSearch.NeedsOnlyOneRequirementForAttack)
                {
                    IgnoreGold = (UserSettings.ActiveSearch.MinGold == 0);
                    IgnoreElixir = (UserSettings.ActiveSearch.MinElixir == 0);
                }

                TownHall townHall = TownHall.Find(CacheBehavior.Default);

                if (townHall.CanSnipe())
                {
                    //The TH is positioned so we might be able to snipe it.
                    Log.Info($"[Human Barch] Sniping Active Town Hall!");
                    return 1;
                }
                else
                {
                    Log.Info($"[Human Barch] Skipping Active Base, TH is not Snipable.");
                    //This is a live base, and we can't snipe the TH.  Ignore the Loot Requirements, and always Skip.
                    returnVal = 0;
                }
            }

            return returnVal;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info($"[Human Barch] Deploy start - V.{Assembly.GetExecutingAssembly().GetName().Version.ToString()}");

            var waveCounter = 1;

            //Check if we can snipe the town hall, and if so, what are the Deployment points for Gruns/Ranged.
            TownHall townHall = TownHall.Find(CacheBehavior.Default);

            Target townHallTarget = townHall.GetSnipeDeployPoints();

            // Get starting resources
            LootResources preLoot = Opponent.GetAvailableLoot();

            if (preLoot == null)
            {
                Log.Error("[Human Barch] Could not read available starting loot");
                Attack.Surrender();
                yield break;
            }
            Log.Info($"[Human Barch] Pre-attack resources - G: {preLoot.Gold}, E: {preLoot.Elixir}, DE: {preLoot.DarkElixir}");

            var collectorCacheBehavior = CacheBehavior.Default;
            var collectorCount = 0;
            var isDead = Opponent.IsDead(true);

            // Loop until surrender conditions are met
            while (true)
            {

                // Get all the units available
                Log.Info($"[Human Barch] Scanning troops for wave {waveCounter}");

                var allElements = Attack.GetAvailableDeployElements();
                var deployElements = allElements.Where(x => x.UnitData != null).ToArray();
                var rangedUnits = deployElements.Where(x => x.IsRanged == true && x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Damage);
                var gruntUnits = deployElements.Where(x => x.IsRanged == false && x.ElementType == DeployElementType.NormalUnit && x.UnitData.AttackType == AttackType.Damage);
                List<DeployElement> king = allElements.Where(x => x.IsHero && x.Name.ToLower().Contains("king")).ToList();
                List<DeployElement> queen = allElements.Where(x => x.IsHero && x.Name.ToLower().Contains("queen")).ToList();
                List<DeployElement> allHeroes = new List<DeployElement>();
                allHeroes.AddRange(king);
                allHeroes.AddRange(queen);

                bool watchHeroes = false;

                //Dont Deploy any Tank Units... even if we have them.

                if (!isDead)
                {
                    if (townHallTarget.ValidTarget)
                    {
                        //Before we enter the main attack routine... If there is an exposed TH, Snipe it.
                        //If there are Teslas around it, oh well. we only spent 9-12 units  of each type trying.
                        if (gruntUnits.Any())
                        {
                            var gruntsToDeploy = Rand.Int(5, 15);
                            Log.Info($"[Human Barch] Sniping Town Hall {gruntsToDeploy} Grunts Near: X:{townHallTarget.DeployGrunts.X} Y:{townHallTarget.DeployGrunts.Y}");
                            foreach (var t in Deploy.AtPoints(gruntUnits.FilterTypesByCount(), townHallTarget.DeployGrunts.RandomPointsInArea(_thDeployRadius, gruntsToDeploy), 1, Rand.Int(10, 40), Rand.Int(10, 40)))
                                yield return t;
                            //Wait almost a second
                            yield return Rand.Int(300, 500); //Wait 
                        }

                        if (rangedUnits.Any())
                        {
                            var rangedToDeploy = Rand.Int(5, 15);
                            Log.Info($"[Human Barch] Sniping Town Hall {rangedToDeploy} Ranged Near: X:{townHallTarget.DeployRanged.X} Y:{townHallTarget.DeployRanged.Y}");
                            foreach (var t in Deploy.AtPoints(rangedUnits.FilterTypesByCount(), townHallTarget.DeployRanged.RandomPointsInArea(_thDeployRadius, rangedToDeploy), 1, Rand.Int(10, 40), Rand.Int(10, 40)))
                                yield return t;
                            //Wait almost a second
                            yield return Rand.Int(300, 500); //Wait 
                        }

                        //If we dont have a star yet, Drop the King...
                        if (!Attack.HaveAStar())
                        { 
                            if (UserSettings.UseKing && king.Any())
                            {
                                Log.Info($"[Human Barch] Deploying King at: X:{townHallTarget.DeployGrunts.X} Y:{townHallTarget.DeployGrunts.Y}");
                                foreach(var t in Deploy.AtPoint(king[0], townHallTarget.DeployGrunts))
                                        yield return t;
                                yield return Rand.Int(900, 1000); //Wait 

                                watchHeroes = true;
                                
                            }

                            //Deploy the Queen
                            if (UserSettings.UseQueen && queen.Any())
                            {
                                Log.Info($"[Human Barch] Deploying Queen at: X:{townHallTarget.DeployRanged.X} Y:{townHallTarget.DeployRanged.Y}");
                                foreach (var t in Deploy.AtPoint(queen[0], townHallTarget.DeployRanged))
                                    yield return t;
                                yield return Rand.Int(900, 1000); //Wait
                                
                                watchHeroes = true;
                            }

                            if (watchHeroes) {
                                //Watch Heros and Hit ability when they get low.
                                Deploy.WatchHeroes(allHeroes);
                                watchHeroes = false; //Only do this once through the loop.
                            }

                        }

                        //Only try once to snipe the town hall when deploying waves.
                        townHallTarget.ValidTarget = false;
                    }

                }
                else
                {
                    //First time through use cached... after the first wave always recheck for Destroyed ones...
                    Target[] targets = HumanLikeAlgorithms.GenerateTargets(_minimumAttackDistanceToCollectors, IgnoreGold, IgnoreElixir, collectorCacheBehavior);
                    collectorCount = targets.Length;

                    //Reorder the Deploy points so they look more human like when attacking.
                    var groupedTargets = targets.ReorderToClosestNeighbor().GroupCloseTargets();

                    collectorCacheBehavior = CacheBehavior.CheckForDestroyed;

                    if (collectorCount < 1)
                    {
                        Log.Info($"[Human Barch] Surrendering - Collectors Remaining = {collectorCount}");

                        // Wait for the wave to finish
                        Log.Info("[Human Barch] Deploy done. Waiting to finish...");
                        var x = Attack.WatchResources(10d).Result;

                        break;
                    }

                    if (townHallTarget.ValidTarget)
                    {
                        //Drop some Grunt and Ranged troups on the TH as well as collectors.
                        //If there are Teslas around it, oh well. we only spent 9-12 units  of each type trying.
                        if (gruntUnits.Any())
                        {
                            var gruntsToDeploy = Rand.Int(4, 6);
                            Log.Info($"[Human Barch] + TH Snipe Dead {gruntsToDeploy} Grunts Near: X:{townHallTarget.DeployGrunts.X} Y:{townHallTarget.DeployGrunts.Y}");
                            foreach (var t in Deploy.AtPoints(gruntUnits.FilterTypesByCount(), townHallTarget.DeployGrunts.RandomPointsInArea(_thDeployRadius, gruntsToDeploy), 1, Rand.Int(10, 40), Rand.Int(10, 40)))
                                yield return t;
                            yield return Rand.Int(300, 500); //Wait 
                        }

                        if (rangedUnits.Any())
                        {
                            var rangedToDeploy = Rand.Int(4, 6);
                            Log.Info($"[Human Barch] + TH Snipe Dead {rangedToDeploy} Ranged Near: X:{townHallTarget.DeployRanged.X} Y:{townHallTarget.DeployRanged.Y}");
                            foreach (var t in Deploy.AtPoints(rangedUnits.FilterTypesByCount(), townHallTarget.DeployRanged.RandomPointsInArea(_thDeployRadius, rangedToDeploy), 1, Rand.Int(10, 40), Rand.Int(10, 40)))
                                yield return t;
                            yield return Rand.Int(300, 500); //Wait 
                        }

                        //Only do this once.
                        townHallTarget.ValidTarget = false;
                    }

                    //Determine the index of the 1st and 2nd largest set of targets all in a row.
                    var largestSetIndex = -1;
                    int largestSetCount = 0;
                    var secondLargestSetIndex = -1;
                    int secondLargestSetCount = 0;

                    for (int i = 0; i < groupedTargets.Count; i++)
                    {
                        if (groupedTargets[i].Length > largestSetIndex)
                        {
                            secondLargestSetCount = largestSetCount;
                            secondLargestSetIndex = largestSetIndex;
                            largestSetCount = groupedTargets[i].Length;
                            largestSetIndex = i;
                        }
                        else if (groupedTargets[i].Length > secondLargestSetIndex)
                        {
                            secondLargestSetCount = groupedTargets[i].Length;
                            secondLargestSetIndex = i;
                        }
                    }

                    Log.Info($"[Human Barch] {groupedTargets.Count} Target Groups, Largest has {largestSetCount} targets, Second Largest {secondLargestSetCount} targets.");

                    //Deploy Barch Units - In Groups on Sets of collectors that are close together.
                    for (int p = 0; p < groupedTargets.Count; p++)
                    {
                        //Deploy Grunts on the Set of Targets.
                        for (int i = 0; i < groupedTargets[p].Length; i++)
                        {
                            var gruntDeployPoint = groupedTargets[p][i].DeployGrunts;

                            if (gruntUnits.Any())
                            {
                                int decreaseFactor = 0;
                                if (i > 0)
                                    decreaseFactor = (int)Math.Ceiling(i / 2d);

                                var gruntsAtCollector = (Rand.Int(6, 8) - decreaseFactor);
                                Log.Info($"[Human Barch] {gruntsAtCollector} Grunts Around Point: X:{gruntDeployPoint.X} Y:{gruntDeployPoint.Y}");
                                foreach (var t in Deploy.AtPoints(gruntUnits.FilterTypesByCount(), gruntDeployPoint.RandomPointsInArea(_collectorDeployRadius, gruntsAtCollector), 1, Rand.Int(10, 40)))
                                    yield return t;
                                yield return Rand.Int(10, 40); //Wait
                            }
                        }
                        
                        //Pause inbetween switching units.
                        yield return Rand.Int(90, 100); //Wait

                        if (secondLargestSetIndex == p && secondLargestSetCount >= 3) {
                            //We are currently deploying to the 2nd largest set of Targets - AND its a set of 3 or more.
                            //Drop the King on the 2nd Target in the set.

                            if (UserSettings.UseKing && king.Any())
                            {
                                Log.Info($"[Human Barch] Deploying King at: X:{groupedTargets[p][1].DeployGrunts.X} Y:{groupedTargets[p][1].DeployGrunts.Y}");
                                foreach (var t in Deploy.AtPoint(king[0], groupedTargets[p][1].DeployGrunts))
                                    yield return t;
                                yield return Rand.Int(900, 1000); //Wait

                                watchHeroes = true;
                            }
                        }

                        if (largestSetIndex == p && largestSetCount >= 3)
                        {
                            //We are currently deploying to the largest set of Targets - AND its a set of 3 or more.
                            //Drop the Queen on the 2nd Target in the set.

                            if (UserSettings.UseQueen && queen.Any())
                            {
                                yield return Rand.Int(90, 100); //Wait before dropping Queen

                                Log.Info($"[Human Barch] Deploying Queen at: X:{groupedTargets[p][1].DeployRanged.X} Y:{groupedTargets[p][1].DeployRanged.Y}");
                                foreach (var t in Deploy.AtPoint(queen[0], groupedTargets[p][1].DeployRanged))
                                    yield return t;
                                yield return Rand.Int(900, 1000); //Wait

                                watchHeroes = true;
                            }
                        }

                        if (watchHeroes)
                        {
                            //Watch Heros and Hit ability when they get low.
                            Deploy.WatchHeroes(allHeroes);
                            watchHeroes = false; //Only do this once through the loop.
                        }


                        //Deploy Ranged units on same set of Targets.
                        for (int i = 0; i < groupedTargets[p].Length; i++)
                        {
                            var rangedDeployPoint = groupedTargets[p][i].DeployRanged;

                            if (rangedUnits.Any())
                            {
                                int decreaseFactor = 0;
                                if (i > 0)
                                    decreaseFactor = (int)Math.Ceiling(i / 2d);

                                var rangedAtCollector = (Rand.Int(5, 7) - decreaseFactor);
                                Log.Info($"[Human Barch] {rangedAtCollector} Ranged Around Point: X:{rangedDeployPoint.X} Y:{rangedDeployPoint.Y}");
                                foreach (var t in Deploy.AtPoints(rangedUnits.FilterTypesByCount(), rangedDeployPoint.RandomPointsInArea(_collectorDeployRadius, rangedAtCollector), 1, Rand.Int(10, 40)))
                                    yield return t;
                                yield return Rand.Int(40, 50); //Wait
                            }
                        }

                        yield return Rand.Int(90, 100); //Wait before switching units back to Grutns and deploying on next set of targets.
                    }
                }

                //Never deploy any Healing type Units.


                //wait a random number of seconds before the next round on all Targets...
                yield return Rand.Int(2000, 5000);

                // Get starting resources, cache needs to be false to force a new check
                LootResources postLoot = Opponent.GetAvailableLoot(false);
                if (postLoot == null)
                {
                    Log.Warning($"[Human Barch] Human Barch Deploy could not read available loot this wave");
                    postLoot = new LootResources() { Gold = -1, Elixir = -1, DarkElixir = -1 };
                }

                Log.Info($"[Human Barch] Wave {waveCounter} resources - G: {postLoot.Gold}, E: {postLoot.Elixir}, DE: {postLoot.DarkElixir}");
                int newGold = preLoot.Gold - postLoot.Gold;
                int newElixir = preLoot.Elixir - postLoot.Elixir;
                int newDark = preLoot.DarkElixir - postLoot.DarkElixir;
                Log.Info($"[Human Barch] Wave {waveCounter} resource diff - G: {newGold}, E: {newElixir}, DE: {newDark}, Collectors: {collectorCount}");

                if (isDead)
                {
                    if (postLoot.Gold + postLoot.Elixir + postLoot.DarkElixir >= 0)
                    {
                        if (newGold + newElixir < 3000 * collectorCount)
                        {
                            Log.Info("[Human Barch] Surrendering because gained resources isn't enough");
                            break;
                        }
                        preLoot = postLoot;
                    }
                }
                else
                {
                    if (Attack.HaveAStar())
                    {
                        Log.Info("[Human Barch] We have a star! TH Sniped!");

                        //Check the Delta in Resources.
                        if (newGold + newElixir < (preLoot.Gold + preLoot.Elixir) * .05f) //Less than 5% of what is available.
                        {
                            //Switch the attack mode to Dead - so we get some of the collectors.
                            Log.Info($"[Human Barch] Not much loot gained from Snipe(G:{newGold} E:{newElixir} out of G:{preLoot.Gold} E:{preLoot.Elixir}) - Try to Loot Collectors also...");
                            isDead = true;
                        }
                        else
                        {
                            //Halt the Attack.
                            break;
                        }
                    }

                    if (waveCounter > 10)
                    {
                        Log.Info("[Human Barch] Fail! TH Not Sniped! our troops died - Surrendering...");
                        break;
                    }
                }

                waveCounter++;
            }

            //TODO - Can we destroy some trash buildings to get a star if we dont already have one?

            //Last thing Call ZapDarkElixterDrills... This uses the Clashbot settings for when to zap, and what level drills to zap.
            Log.Info("[Human Barch] Checking to see if we can Zap DE Drills...");
            foreach (var t in ZapDarkElixirDrills())
                yield return t;

            //We broke out of the attack loop... 
            Attack.Surrender();
        }

    }
}

