using Dalamud.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OhGeeCD.Model
{
    [Serializable]
    public class Job : IDisposable
    {
        public Job(uint id, string name, string? parent = null, string? jobname = null, string? parentJobName = null)
        {
            Id = id;
            Abbreviation = name;
            ParentAbbreviation = parent;
            Name = NameToUpper(jobname);
            ParentName = NameToUpper(parentJobName);
        }

        [JsonIgnore]
        public string Abbreviation { get; private set; }

        [JsonProperty]
        public List<OGCDAction> Actions { get; set; } = new List<OGCDAction>();

        [JsonProperty]
        public uint Id { get; set; }

        [JsonIgnore]
        public uint Level { get; private set; }

        [JsonIgnore]
        public string? Name { get; set; }

        [JsonIgnore]
        public string NameOrParentName => Name == ParentName ? Name! : $"{Name} / {ParentName}";

        [JsonIgnore]
        public string? ParentAbbreviation { get; private set; }

        [JsonIgnore]
        public string? ParentName { get; set; }

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

        public void MakeActive(uint level = uint.MaxValue)
        {
            PluginLog.Debug($"Job now active: {Abbreviation}/{ParentAbbreviation}");
            if(level != uint.MaxValue)
                SetLevel(level);
            foreach (var action in Actions)
            {
                action.MakeActive(Level);
            }
        }

        public void MakeInactive()
        {
            foreach (var action in Actions)
            {
                action.MakeInactive();
            }
        }

        public void SetAbbreviation(string abbreviation, string name)
        {
            Abbreviation = abbreviation;
            Name = NameToUpper(name);
        }

        public void SetLevel(uint level)
        {
            PluginLog.Debug($"Setting level of {Abbreviation} to {level}");
            Level = level;
        }

        private string NameToUpper(string? name)
        {
            if (name == null) return string.Empty;
            return string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0].ToString().ToUpper() + s.Substring(1)));
        }
    }
}