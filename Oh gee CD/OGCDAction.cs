using Dalamud.Logging;
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
        [JsonProperty]
        public string SoundPath { get; set; } = string.Empty;

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
        [JsonIgnore]
        public short CurrentStacks { get; private set; }
        readonly CancellationTokenSource cts;
        [JsonIgnore]
        public byte RequiredJobLevel { get; private init; }
        [JsonIgnore]
        public bool IsCurrentClassJob { get; private set; }
        [JsonIgnore]
        public bool IsAvailable => currentJobLevel >= RequiredJobLevel;
        [JsonIgnore]
        public double CooldownTimer { get; private set; }
        private uint currentJobLevel;

        public event EventHandler<SoundEventArgs>? SoundEvent;

        public OGCDAction(uint id, uint icon, string name, TimeSpan recast, byte cooldownGroup, byte requiredLevel, uint currentJobLevel)
        {
            Id = id;
            Icon = icon;
            Name = name;
            Recast = recast;
            TextToSpeechName = name;
            CooldownGroup = (byte)(cooldownGroup - 1);
            RequiredJobLevel = requiredLevel;
            this.currentJobLevel = currentJobLevel;
            MaxStacks = (short)ActionManager.GetMaxCharges(Id, currentJobLevel);
            CurrentStacks = MaxStacks;
            cts = new CancellationTokenSource();
        }

        public void Debug()
        {
            PluginLog.Debug($"Id:{Id} | Name:{Name} | MaxStack:{MaxStacks} | CD:{Recast.TotalSeconds}s | ReqLevel:{RequiredJobLevel} | CanCast:{IsAvailable}");
        }

        public void SetTextToSpeechName(string newname)
        {
            TextToSpeechName = newname;
        }

        private bool timerRunning = false;
        private int soundQueue = 1;

        public unsafe void StartCountdown(ActionManager* actionManager)
        {
            Task.Run(() =>
            {
                var detail = actionManager->GetRecastGroupDetail(CooldownGroup);
                if (detail->IsActive == 1 && timerRunning)
                {
                    soundQueue++;
                    PluginLog.Debug("Recast timer is active for " + Name);
                    return;
                }

                PluginLog.Debug("First cast " + Name + "(" + CooldownGroup + ")");

                soundQueue = 1;
                timerRunning = true;
                CooldownTimer = 0;
                bool earlyCallOutReset = true;
                Thread.Sleep(1000);
                do
                {
                    Thread.Sleep(100);
                    detail = actionManager->GetRecastGroupDetail(CooldownGroup);

                    var curTimeElapsed = detail->Elapsed;

                    earlyCallOutReset = earlyCallOutReset || CooldownTimer < ((Recast.TotalSeconds * MaxStacks - curTimeElapsed) % Recast.TotalSeconds);
                    CooldownTimer = ((Recast.TotalSeconds * MaxStacks - curTimeElapsed) % Recast.TotalSeconds);

                    var stacks = (short)Math.Floor(detail->Elapsed / Recast.TotalSeconds);

                    if (IsCurrentClassJob
                        && ((soundQueue > 1 && stacks > CurrentStacks && EarlyCallout == 0.0)
                           || (CooldownTimer <= EarlyCallout && soundQueue >= 1 && earlyCallOutReset)))
                    {
                        PlaySound();
                        if (CooldownTimer <= EarlyCallout && soundQueue >= 1 && earlyCallOutReset) earlyCallOutReset = false;
                        if (soundQueue > 0) soundQueue--;
                    }

                    CurrentStacks = stacks;

                } while (detail->IsActive == 1 && !cts.IsCancellationRequested && CurrentStacks != MaxStacks);

                CurrentStacks = MaxStacks;
                if (IsCurrentClassJob && soundQueue == 1)
                {
                    PlaySound();
                }

                CooldownTimer = 0;
                timerRunning = false;
            }, cts.Token);
        }

        private unsafe void PlaySound()
        {
            SoundEvent?.Invoke(this, new SoundEventArgs(TextToSpeechEnabled ? TextToSpeechName : null,
                SoundEffectEnabled ? SoundEffect : null,
                SoundEffectEnabled ? SoundPath : null));
        }

        private void ReduceStacks()
        {
            CurrentStacks--;
            PluginLog.Debug($"Reducing stacks for {Name} to {CurrentStacks}");
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
            CurrentStacks = MaxStacks;
        }

        public void UpdateValuesFromOtherAction(OGCDAction fittingActionFromConfig)
        {
            OGCDBarId = fittingActionFromConfig.OGCDBarId;
            DrawOnOGCDBar = fittingActionFromConfig.DrawOnOGCDBar;
            SoundEffect = fittingActionFromConfig.SoundEffect;
            SoundEffectEnabled = fittingActionFromConfig.SoundEffectEnabled;
            TextToSpeechName = fittingActionFromConfig.TextToSpeechName;
            TextToSpeechEnabled = fittingActionFromConfig.TextToSpeechEnabled;
            EarlyCallout = fittingActionFromConfig.EarlyCallout;
            SoundPath = fittingActionFromConfig.SoundPath;
        }
    }
}