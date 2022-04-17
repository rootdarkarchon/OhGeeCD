﻿using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Oh_gee_CD
{
    [Serializable]
    public class OGCDAction : IDisposable, ISoundSource
    {
        [JsonProperty]
        public uint Id { get; set; }
        [JsonProperty]
        public int OGCDBarId { get; set; } = 0;
        [JsonProperty]
        public string TextToSpeechName { get; set; }
        [JsonProperty]
        public int SoundEffect { get; set; }
        [JsonProperty]
        public bool SoundEffectEnabled { get; set; } = false;
        [JsonProperty]
        public bool TextToSpeechEnabled { get; set; } = false;
        public bool DrawOnOGCDBar { get; set; } = false;
        [JsonProperty]
        public double EarlyCallout { get; set; } = 0;

        [JsonIgnore]
        public uint Icon { get; set; }
        [JsonIgnore]
        public string Name { get; set; }
        [JsonIgnore]
        public TimeSpan Recast { get; set; }
        [JsonIgnore]
        public byte CooldownGroup { get; }
        [JsonIgnore]
        public short MaxStacks { get; private set; }
        private short currentStacks;
        readonly CancellationTokenSource cts;
        [JsonIgnore]
        public byte RequiredJobLevel { get; private init; }
        [JsonIgnore]
        public bool IsCurrentClassJob { get; private set; }
        [JsonIgnore]
        public bool IsAvailable => currentJobLevel >= RequiredJobLevel;
        private uint currentJobLevel;

        public event EventHandler<SoundEventArgs>? SoundEvent;

        public OGCDAction(uint id, uint icon, string name, TimeSpan recast, byte cooldownGroup, byte requiredLevel, uint currentJobLevel)
        {
            Id = id;
            Icon = icon;
            Name = name;
            Recast = recast;
            TextToSpeechName = name;
            CooldownGroup = cooldownGroup;
            RequiredJobLevel = requiredLevel;
            this.currentJobLevel = currentJobLevel;
            MaxStacks = (short)ActionManager.GetMaxCharges(Id, currentJobLevel);
            currentStacks = MaxStacks;
            cts = new CancellationTokenSource();
        }

        public void Debug()
        {
            PluginLog.Debug($"Id:{Id} | Name:{Name} | MaxStack:{MaxStacks} | CD:{Recast.TotalSeconds}s | ReqLevel:{RequiredJobLevel} | CanCast:{IsAvailable}");
        }

        public void StartCountdown()
        {
            StartCountdown(TimeSpan.Zero);
        }

        public void TriggerAdditionalCountdown(TimeSpan timeSpan)
        {
            StartCountdown(timeSpan);
        }

        public void SetTextToSpeechName(string newname)
        {
            TextToSpeechName = newname;
        }

        private void StartCountdown(TimeSpan timerOverride)
        {
            TimeSpan recastTimer = Recast;
            if (timerOverride != TimeSpan.Zero)
            {
                recastTimer = timerOverride;
                PluginLog.Debug($"Triggered in addition: {Name}");
            }
            else
            {
                PluginLog.Debug($"Casted {Name}");
            }

            if (MaxStacks > 1 && currentStacks != MaxStacks)
            {
                ReduceStacks();
            }
            else
            {
                Task countdown = new(async () =>
                {
                    try
                    {
                        ReduceStacks();
                        do
                        {
                            PluginLog.Debug($"Looping for {Name}: {currentStacks}/{MaxStacks}, from now: +{recastTimer.TotalSeconds}s");
                            await Task.Delay((int)(recastTimer.TotalMilliseconds - TimeSpan.FromSeconds(EarlyCallout).TotalMilliseconds), cts.Token);
                            if (IsCurrentClassJob && timerOverride == TimeSpan.Zero)
                            {
                                SoundEvent?.Invoke(this, new SoundEventArgs(TextToSpeechEnabled ? TextToSpeechName : "", SoundEffectEnabled ? SoundEffect : -1));
                            }
                            await Task.Delay((int)(recastTimer.TotalMilliseconds - (recastTimer.TotalMilliseconds - TimeSpan.FromSeconds(EarlyCallout).TotalMilliseconds)), cts.Token);
                            PluginLog.Debug($"{Name} available again!");
                            currentStacks++;

                        } while (currentStacks != MaxStacks && !cts.IsCancellationRequested);
                    }
                    catch (TaskCanceledException)
                    {
                        PluginLog.Debug($"Task for {Name} cancelled");
                    }
                }, cts.Token);

                countdown.Start();
            }
        }

        private void ReduceStacks()
        {
            currentStacks--;
            PluginLog.Debug($"Reducing stacks for {Name} to {currentStacks}");
        }

        public void Dispose()
        {
            IsCurrentClassJob = false;
            cts.Cancel();
        }

        public void MakeInactive()
        {
            IsCurrentClassJob = false;
        }

        public void MakeActive(uint currentJobLevel)
        {
            IsCurrentClassJob = true;
            this.currentJobLevel = currentJobLevel;
            MaxStacks = (short)ActionManager.GetMaxCharges(Id, currentJobLevel);
        }

        public void UpdateValuesFromOtherAction(OGCDAction fittingActionFromConfig)
        {
            OGCDBarId = fittingActionFromConfig.OGCDBarId;
            SoundEffect = fittingActionFromConfig.SoundEffect;
            SoundEffectEnabled = fittingActionFromConfig.SoundEffectEnabled;
            TextToSpeechName = fittingActionFromConfig.TextToSpeechName;
            TextToSpeechEnabled = fittingActionFromConfig.TextToSpeechEnabled;
            EarlyCallout = fittingActionFromConfig.EarlyCallout;
        }
    }
}