using System.Collections.Generic;
using System.Drawing;
using CoC_Bot.API;

namespace LavaLoonDeploy
{
    class DeployCalculationInformation
    {
        public PointFT IntersectionPointFT;
        public PointF IntersectionPointF;
        public List<PointFT> AllDeployPoints;

        public DeployCalculationInformation(PointFT intersectionPointFt, PointF intersectionPointF, List<PointFT> allDeployPoints)
        {
            IntersectionPointFT = intersectionPointFt;
            IntersectionPointF = intersectionPointF;
            AllDeployPoints = allDeployPoints;
        }
    }
}