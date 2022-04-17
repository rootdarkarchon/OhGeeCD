using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Oh_gee_CD
{
    [Serializable]
    public class OGCDAction : IDisposable
    {
        public uint Id { get; set; }
        public int OGCDBarId { get; set; }
        public string TextToSpeechName { get; set; }

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
        public bool IsAvailable => currentJobLevel >= RequiredJobLevel;
        private uint currentJobLevel;

        public event EventHandler<CooldownTriggeredEventArgs>? CooldownTriggered;

        public OGCDAction(uint id, string name, TimeSpan recast, byte cooldownGroup, byte requiredLevel, uint currentJobLevel)
        {
            Id = id;
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
                            await Task.Delay((int)recastTimer.TotalMilliseconds, cts.Token);
                            if (IsCurrentClassJob)
                            {
                                CooldownTriggered?.Invoke(this, new CooldownTriggeredEventArgs(TextToSpeechName, 0));
                            }
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

    }
}