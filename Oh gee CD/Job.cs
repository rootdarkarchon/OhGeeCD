using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OhGeeCD
{
    public class Job : IDisposable
    {
        public string Abbreviation { get; private set; }
        public string? ParentAbbreviation { get; private set; }

        public bool IsActive { get; private set; }

        public uint Level { get; private set; }

        public void SetLevel(uint level)
        {
            PluginLog.Debug($"Setting level of {Abbreviation} to {level}");
            Level = level;
        }

        public void SetAbbreviation(string abbreviation)
        {
            Abbreviation = abbreviation;
        }

        public Job(string name, string? parent = null)
        {
            Abbreviation = name;
            ParentAbbreviation = parent;
        }

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
            foreach (var action in Actions.OrderBy(a=>a.RequiredJobLevel))
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
    }
}