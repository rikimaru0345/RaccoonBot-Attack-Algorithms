using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomAlgorithmSettings
{
    /// <summary>
    /// Class used only for serializing the current value and type of setting.
    /// </summary>
    internal class Setting
    {
        public string Name { get; set; }

        public int Value { get; set; }

        public SettingInstanceType InstanceType { get; set; }

    }
}
