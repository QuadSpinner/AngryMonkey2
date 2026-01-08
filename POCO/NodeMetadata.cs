using System;
using System.Collections.Generic;
using System.Text;

namespace AngryMonkey.POCO
{
    public class NodeMetadata
    {
        public long MaxPorts { get; set; }
        public bool IsSoftData { get; set; }
        public bool IsHidden { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Family { get; set; }
        public string FamilyIcon { get; set; }
        public string Toolbox { get; set; }
        public string Classification { get; set; }
        public string[] Keywords { get; set; }
        public string ShortCode { get; set; }
        public long Usage { get; set; }
        public string SearchString { get; set; }
        public string[] SearchWords { get; set; }
        public bool RequiresBaking { get; set; }
        public bool UsesAccumulation { get; set; }
        public string? AccumulationType { get; set; }
        public bool HasCustomColorization { get; set; }
        public bool HasCustomUnderlay { get; set; }
        public bool CanCreatePorts { get; set; }
        public bool OrderedPorts { get; set; }
        public bool PreventPurge { get; set; }
    }

}
