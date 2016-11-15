using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CoC_Bot;
using CoC_Bot.API;
using Shared;

[assembly: Addon("RandomDeploy", "Randomly scatter troops across the screen", "Eric Lindsey")]

namespace RandomDeploy
{
    [AttackAlgorithm("Random Deploy", "Scatters troops randomly across the enemy base")]
    public class RandomDeploy : BaseAttack
    {
        public RandomDeploy(Opponent opponent) : base(opponent)
        {
            // Default behavior
        }

        public override double ShouldAccept()
        {
            // check if the base meets the user's requirements
            if (!Opponent.MeetsRequirements(BaseRequirements.All))
            {
                Log.Debug($"[Random] Skipping this base because it doesn't meet the requirements.");
                return 0;
            }
            return 0.7;
        }

        public override IEnumerable<int> AttackRoutine()
        {
            Log.Info("[Random] Attack start");

            // Get all units, regardless of type
            List<DeployElement> units = Deploy.GetTroops();

            // Scatter them across the map
            Random rng = new Random();
            foreach (DeployElement unit in units)
            {
                Log.Info("[Random] Deploying " + unit.PrettyName);
                for (int i = 0; i < 2; i++)
                    Input.Click(unit.Rect.GetCenter());

                while (unit.Count > 0)
                {
                    Log.Debug($"Trying to deploy {unit.Count:N0} {unit.PrettyName}s");
                    for (int i = 0; i < unit.Count; i++)
                    {
                        Point p = new PointFT(
                            (float)rng.Range(PointFT.MinRedZoneX, PointFT.MaxRedZoneX),
                            (float)rng.Range(PointFT.MinRedZoneY, PointFT.MaxRedZoneY))
                            .ToScreenAbsolute();
                        Input.Click(p);
                    }
                    int deployed = unit.Recount();
                    Log.Debug($"Deployed {deployed:N0} {unit.PrettyName}s");
                }

                yield return 1000;
            }

            // Watch for hero health etc.
            Deploy.WatchHeroes(units.Where(u => u.IsHero).ToList());

            Log.Info("[Random] Deploy done");
        }

        public override string ToString()
        {
            return "Random Deploy";
        }
    }
}
