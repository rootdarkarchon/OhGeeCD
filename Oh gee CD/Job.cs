using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oh_gee_CD
{
    [Serializable]
    public class Job : IDisposable
    {
        [JsonProperty]
        public string Abbreviation { get; private set; }

        [JsonIgnore]
        public string? Name { get; set; }

        [JsonIgnore]
        public string? ParentName { get; set; }

        [JsonIgnore]
        public string NameOrParentName => Name == ParentName ? Name! : $"{Name} / {ParentName}";

        [JsonIgnore]
        public string? ParentAbbreviation { get; private set; }

        [JsonIgnore]
        public bool IsActive { get; private set; }

        [JsonIgnore]
        public uint Level { get; private set; }

        public void SetLevel(uint level)
        {
            PluginLog.Debug($"Setting level of {Abbreviation} to {level}");
            Level = level;
        }

        public void SetAbbreviation(string abbreviation, string name)
        {
            Abbreviation = abbreviation;
            Name = NameToUpper(name);
        }

        public Job(string name, string? parent = null, string? jobname = null, string? parentJobName = null)
        {
            Abbreviation = name;
            ParentAbbreviation = parent;
            Name = NameToUpper(jobname);
            ParentName = NameToUpper(parentJobName);
        }

        [JsonProperty]
        public List<OGCDAction> Actions { get; set; } = new List<OGCDAction>();

        public void MakeActive()
        {
            PluginLog.Debug($"Job now active: {Abbreviation}/{ParentAbbreviation}");
            IsActive = true;
            foreach (var action in Actions)
            {
                action.MakeActive(Level);
            }
        }

        public void MakeInactive()
        {
            IsActive = false;
            foreach (var action in Actions)
            {
                action.MakeInactive();
            }
        }

        public void Debug()
        {
            PluginLog.Debug($"{Abbreviation} ({ParentAbbreviation}) Lvl {Level}");
            foreach (var action in Actions)
            {
                action.Debug();
            }
        }

        public void Dispose()
        {
            foreach (var action in Actions)
            {
                action.Dispose();
            }
        }

        private string NameToUpper(string? name)
        {
            if (name == null) return string.Empty;
            return string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0].ToString().ToUpper() + s.Substring(1)));
        }
    }
}