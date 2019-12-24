using System.Collections.Generic;

namespace Twino.Mvc.Explorer.Models
{
    internal class ControllerInfo
    {
        public string Name { get; set; }
        
        public List<ActionInfo> Actions { get; set; }
    }
}