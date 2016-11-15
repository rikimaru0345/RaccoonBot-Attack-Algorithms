using CoC_Bot.API;
using CoC_Bot.API.Buildings;

namespace SharedCode
{
    public class Target
    {
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
