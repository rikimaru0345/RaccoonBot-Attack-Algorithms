using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using CoC_Bot;
using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using CoC_Bot.Internals;
using CoC_Bot.Modules;
using CoC_Bot.Modules.AttackAlgorithms;

[assembly: Addon("MilkingDeploy Addon", "Contains the milking deploy algorithm", "BoostBotTeam")]

namespace MilkingDeploy
{
    [AttackAlgorithm("MilkingDeploy", "Deploys units close as possible to collectors and saving as many units as possible.")]
    class MilkingDeploy : BaseAttack
    {
        private PointFT[] deployPoints;
        private ResourcesFull resourcesFull;

        public MilkingDeploy(Opponent opponent) : base(opponent) { }

        public override string ToString()
        {
            return "Milking Deploy";
        }

        public override double ShouldAccept()
        {
            // set flags to only check elixir and gold against the user's settings
            var requirementsToCheck = BaseRequirements.Elixir | BaseRequirements.Gold;

            // check if the base meets the user's requirements
            if (!Opponent.MeetsRequirements(requirementsToCheck))
            {
                Log.Info($"[Attack] Skipping this base because it doesn't meet the loot requirements.");
                return 0;
            }

            resourcesFull = GetResourcesState();
            GenerateDeployPointsFromMinesToMilk();
            Log.Debug($"{deployPoints.Length} collectors found outside.");

            if (deployPoints.Length < 2)
            {
                Log.Info($"[Attack] Skipping this base because {deployPoints.Length} collectors were found outside the wall");
                return 0;
            }
            return 1;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[Deploy] Deploy start");

            // Get all the units available
            Log.Debug("Scanning troops");
            var deployElements = Deploy.GetTroops();
            var spells = deployElements.Extract(DeployElementType.Spell);
            var tankUnits = deployElements.Extract(AttackType.Tank).ToArray();
            var attackUnits = deployElements.Extract(AttackType.Damage).ToArray();
            var healUnits = deployElements.Extract(AttackType.Heal).ToArray();

            var waveCounter = 1;

            // Get starting resources
            LootResources preLoot = Opponent.GetAvailableLoot();

            if (preLoot == null)
            {
                Log.Error("[Deploy] Milking deploy could not read available starting loot");
                Attack.Surrender();
                yield break;
            }

            Log.Debug($"[Deploy] Pre-attack resources - G: {preLoot.Gold}, E: {preLoot.Elixir}, DE: {preLoot.DarkElixir}");

            // Make sure we wait at least 15 seconds in this attack, in case we snipe TH

            // Loop until surrender conditions are met
            while (true)
            {
                // Get deploy points for each mine that is on the outside of the base
                //resourcesFull = GetResourcesState();
                GenerateDeployPointsFromMinesToMilk(CacheBehavior.CheckForDestroyed);
                if (deployPoints == null || deployPoints.Length < 1)
                {
                    Log.Debug("Surrendering because deployPoints = " + deployPoints?.Length);
                    break;
                }

                if (tankUnits.Any())
                {
                    foreach (var t in Deploy.AtPoints(tankUnits, deployPoints))
                        yield return t;
                    yield return 1000;
                }

                if (attackUnits.Any())
                {
                    foreach (var t in Deploy.AtPoints(attackUnits, deployPoints, 6))
                        yield return t;
                    yield return 1000;
                }

                if (healUnits.Any())
                {
                    foreach (var t in Deploy.AtPoints(healUnits, deployPoints))
                        yield return t;
                    yield return 1000;
                }

                // Wait for the wave to finish
                Log.Info($"[Deploy] Wave {waveCounter} Deployed. Waiting to finish...");
                foreach (var t in Attack.WaitForNoResourceChange())
                    yield return t;

                // Get starting resources, cache needs to be false to force a new check
                LootResources postLoot = Opponent.GetAvailableLoot(false);

                if (postLoot == null)
                {
                    Log.Warning("[Deploy] Milking Deploy could not read available loot this wave");
                }
                else
                {
                    Log.Debug($"[Deploy] Wave {waveCounter} resources - G: {postLoot.Gold}, E: {postLoot.Elixir}, DE: {postLoot.DarkElixir}");
                    int newGold = preLoot.Gold - postLoot.Gold;
                    int newElixir = preLoot.Elixir - postLoot.Elixir;
                    int newDark = preLoot.DarkElixir - postLoot.DarkElixir;
                    Log.Debug($"[Deploy] Wave {waveCounter} resource diff - G: {newGold}, E: {newElixir}, DE: {newDark}, points: {deployPoints.Length}");
                    if (newGold + newElixir < 5000 * deployPoints.Length)
                    {
                        Log.Debug("Surrendering because gained resources isn't enough");
                        break;
                    }
                    preLoot = postLoot;
                }

                waveCounter++;
            }

            // Wait for the wave to finish
            Log.Info("[Deploy] Deploy done. Waiting to finish...");
            foreach (var t in Attack.WaitForNoResourceChange(10))
                yield return t;

            if (spells.Any(u => u.Id == DeployId.Lightning) && DarkElixirDrill.Find(CacheBehavior.ForceScan, 4).Length > 0)
            {
                Log.Debug("Level 4 or greater drills found, waiting for attack to finish.");
                foreach (var t in Attack.WaitForNoResourceChange(5))
                    yield return t;

                foreach (var t in ZapDarkElixirDrills())
                    yield return t;
            }

            Attack.Surrender();
        }

        private void GenerateDeployPointsFromMinesToMilk(CacheBehavior behavior = CacheBehavior.Default)
        {
            // Find all mines
            List<Building> mines = new List<Building>();
            TownHall th = TownHall.Find();
            if (th != null)
                mines.Add(th);
            if (!resourcesFull.HasFlag(ResourcesFull.Gold))
                mines.AddRange(GoldMine.Find(behavior));
            if (!resourcesFull.HasFlag(ResourcesFull.Elixir))
                mines.AddRange(ElixirCollector.Find(behavior));
            if (!resourcesFull.HasFlag(ResourcesFull.Delixir))
                mines.AddRange(DarkElixirDrill.Find(behavior));

            List<PointFT> resultPoints = new List<PointFT>();
            foreach (Building mine in mines)
            {
                PointFT center = mine.Location.GetCenter();
                PointFT closest = GameGrid.RedPoints.OrderBy(p => p.DistanceSq(center)).First();
                float distanceSq = center.DistanceSq(closest);
                Log.Debug("DistanceSq from " + mine.GetType().Name + " to red point: " + distanceSq.ToString("F1"));
                if (distanceSq < 9)  // 3 tiles (squared = 9) means there is no wall or building between us and the collector
                {
                    Log.Debug("Adding deploy point");
                    PointFT awayFromRedLine = closest.AwayFrom(center, 0.5f);
                    resultPoints.Add(awayFromRedLine);
                }
            }
            Log.Debug("Found " + resultPoints.Count + " deploy points");
            deployPoints = resultPoints.ToArray();
        }
    }
}
