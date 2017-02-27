using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomAlgorithmSettings
{
    /// <summary>
    /// Defines the types of groups this setting can belong to.  Active and Dead, will apear on "Active" and "Dead" Property Tabs
    /// "ActiveOnly" shows on the Active Property Tab.
    /// "DeadOnly" shows on the Dead Property Tab.
    /// "Global" shows on the Global Property Tab.
    /// </summary>
    public enum SettingType
    {
        ActiveAndDead = 0
        , ActiveOnly = 1
        , DeadOnly = 2
        , Global = 3
    }
    
    /// <summary>
    /// At Runtime an algorithm setting instance can be either Active/Dead, or Global - meaning applies to both active and Dead Bases.
    /// </summary>
    public enum SettingInstanceType
    {
        Active = 1
        , Dead = 2
        , Global = 3
    }
}
