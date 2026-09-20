using System;
using System.Collections.Generic;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    public class AgentLabDefs
    {
        public Dictionary<String, String> ConditionMap { get; set; }
        public List<String> CommandSizes { get; set; }
        public List<Int32> DeletedCommands { get; set; }
        public Dictionary<String, AgentLabCommandDef> CommandMap { get; set; }
    }
    public class AgentLabCommandDef
    {
        public String Name { get; set; }
        public List<String> Arguments { get; set; }
    }
}
