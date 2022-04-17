using Dalamud.Game.ClientState;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace OhGeeCD
{
    public class OGCDAction : IDisposable
    {
        public string Name { get; set; }
        public uint Id { get; set; }
        public TimeSpan Recast { get; set; }
        public byte CooldownGroup { get; }
        public short MaxStacks { get; private set; }
        private short currentStacks;
        readonly CancellationTokenSource cts;
        private byte requiredLevel;
        private readonly SpeechSynthesizer synthesizer;

        public bool IsCurrentClassJob { get; private set; }
        public bool IsAvailable { get; private set; }

        public void MakeActive(uint jobLevel)
        {
            IsCurrentClassJob = true;
            MaxStacks = (short)ActionManager.GetMaxCharges(Id, jobLevel);
            IsAvailable = jobLevel >= requiredLevel;
        }

        public OGCDAction(uint id, string name, TimeSpan recast, byte cooldownGroup, byte requiredLevel, uint jobLevel, SpeechSynthesizer synthesizer)
        {
            Id = id;
            Name = name;
            Recast = recast;
            CooldownGroup = cooldownGroup;
            this.requiredLevel = requiredLevel;
            this.synthesizer = synthesizer;
            MaxStacks = (short)ActionManager.GetMaxCharges(Id, jobLevel);
            currentStacks = MaxStacks;
            cts = new CancellationTokenSource();
        }

        public void Debug()
        {
            PluginLog.Debug($"{Id}:{Name}:{MaxStacks}:{Recast}:{requiredLevel}:{IsAvailable}");
        }

        public void StartCountdown()
        {
            StartCountdown(TimeSpan.Zero);
        }

        public void TriggerAdditionalCountdown(TimeSpan timeSpan)
        {
            StartCountdown(timeSpan);
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
                                synthesizer.Speak(Name);
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

        internal void MakeInactive()
        {
            IsCurrentClassJob = false;
        }
    }
}