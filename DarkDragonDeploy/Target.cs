using CoC_Bot.API;
using CoC_Bot.API.Buildings;
using SharedCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedCode
{
    public class Target
    {
        //Default Constructor...
        public Target() {
        }

        //Constructor taking an already found building as the target.
        public Target(Building building)
        {
            this.ValidTarget = false;

            this.ValidTarget = true;
            this.TargetBuilding = building;
            this.Center = building.Location.GetCenter();

            this.Edge = HumanLikeAlgorithms.Origin.PointOnLineAwayFromEnd(this.Center, 1f); //TODO Move to Constants..
            this.NearestRedLine = GameGrid.RedPoints.OrderBy(p => p.DistanceSq(this.Edge)).First();
            this.EdgeToRedline = this.Edge.DistanceSq(this.NearestRedLine);

            //Fill the DeployGrunts Property with where out main dragon force should go.
            this.DeployGrunts = HumanLikeAlgorithms.Origin.PointOnLineAwayFromEnd(this.NearestRedLine, 0.2f); //TODO Move to Constants..
            this.DeployRanged = HumanLikeAlgorithms.Origin.PointOnLineAwayFromEnd(this.NearestRedLine, 2.0f); //TODO Move to Constants..
        }


        /// <summary>
        /// Specifies if the target is Valid or not.
        /// </summary>
        public bool ValidTarget { get; set; }

        /// <summary>
        /// Gets the Name of the Type of Target.
        /// </summary>
        public string Name
        {
            get
            {
                var name = TargetBuilding?.GetType()?.Name;

                if (name == null)
                    return string.Empty;
                else
                    return name;
            }
        }

        /// <summary>
        /// The Center of the Target
        /// </summary>
        public PointFT Center { get; set; }

        /// <summary>
        /// The Nearest Redline Point to the Target
        /// </summary>
        public PointFT NearestRedLine { get; set; }

        /// <summary>
        /// Approximate Edge of the Target on the same plane as Center and Redline.
        /// </summary>
        public PointFT Edge { get; set; }

        /// <summary>
        /// The Point to Deploy grunt troops at
        /// </summary>
        public PointFT DeployGrunts { get; set; }

        /// <summary>
        /// The Point to Deploy ranged troops at
        /// </summary>
        public PointFT DeployRanged { get; set; }

        /// <summary>
        /// The Distance from the Center of the Target to nearest Redline.
        /// </summary>
        public float CenterToRedline { get; set; }

        /// <summary>
        /// The Distance from the Center of the Target to the Deploy Point
        /// </summary>
        public float CenterToDeploy { get; set; }

        /// <summary>
        /// The Distance from the Edge of the Target to the Nearest Redline Point
        /// </summary>
        public float EdgeToRedline { get; set; }


        /// <summary>
        /// A Reference to the Target Building.
        /// </summary>
        public Building TargetBuilding { get; set; }
    }
}
