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

        public unsafe void StartCountdown(ActionManager* actionManager)
        {
            Task.Run(() =>
            {
                var detail = actionManager->GetRecastGroupDetail(CooldownGroup);
                if (detail->IsActive == 1 && timerRunning)
                {
                    PluginLog.Debug("Recast timer is active for " + Name);
                    CurrentStacks--;
                    return;
                }

                CurrentStacks--;
                timerRunning = true;
                bool soundPlayed = false;
                var prevCoolDown = CooldownTimer;
                while (detail->IsActive == 1 && !cts.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                    detail = actionManager->GetRecastGroupDetail(CooldownGroup);

                    var curTimeElapsed = detail->Elapsed;

                    CooldownTimer = ((Recast.TotalSeconds * MaxStacks - curTimeElapsed) % Recast.TotalSeconds);

                    if (IsCurrentClassJob && CooldownTimer <= EarlyCallout && !soundPlayed)
                    {
                        soundPlayed = true;
                        SoundEvent?.Invoke(this, new SoundEventArgs(TextToSpeechEnabled ? TextToSpeechName : null,
                            SoundEffectEnabled ? SoundEffect : null,
                            SoundEffectEnabled ? SoundPath : null));
                    }

                    PluginLog.Debug("Looping recast timer active " + Name + ", " + CooldownTimer + ";" + curTimeElapsed);
                    prevCoolDown = CooldownTimer;
                }

                if (IsCurrentClassJob && !soundPlayed)
                {
                    soundPlayed = true;
                    SoundEvent?.Invoke(this, new SoundEventArgs(TextToSpeechEnabled ? TextToSpeechName : null,
                        SoundEffectEnabled ? SoundEffect : null,
                        SoundEffectEnabled ? SoundPath : null));
                }

                CooldownTimer = 0;
                CurrentStacks++;
                timerRunning = false;
            }, cts.Token);
            /*
            TimeSpan recastTimer = Recast;
            if (CurrentStacks == 0) return;
            PluginLog.Debug($"Casted {Name}");

            if (MaxStacks > 1 && CurrentStacks != MaxStacks)
            {
                ReduceStacks();
            }
            else
            {
                Task countdown = new(() =>
                {
                    try
                    {
                        ReduceStacks();
                        do
                        {
                            CooldownTimer = (int)Recast.TotalSeconds;
                            PluginLog.Debug($"Looping for {Name}: {CurrentStacks}/{MaxStacks}, from now: +{recastTimer.TotalSeconds}s");
                            var waitingTime = (int)(recastTimer.TotalMilliseconds - TimeSpan.FromSeconds(EarlyCallout).TotalMilliseconds);
                            while (waitingTime > 0)
                            {
                                Thread.Sleep(100);
                                CooldownTimer -= 0.1;
                                waitingTime -= 100;
                            }
                            //await Task.Delay((int)(recastTimer.TotalMilliseconds - TimeSpan.FromSeconds(EarlyCallout).TotalMilliseconds), cts.Token);
                            if (IsCurrentClassJob)
                            {
                                SoundEvent?.Invoke(this, new SoundEventArgs(TextToSpeechEnabled ? TextToSpeechName : null,
                                    SoundEffectEnabled ? SoundEffect : null,
                                    SoundEffectEnabled ? SoundPath : null));
                            }
                            var remainingWaitingTime = (int)(recastTimer.TotalMilliseconds - (recastTimer.TotalMilliseconds - TimeSpan.FromSeconds(EarlyCallout).TotalMilliseconds));
                            while (remainingWaitingTime > 0)
                            {
                                Thread.Sleep(100);
                                CooldownTimer -= 0.1;
                                remainingWaitingTime -= 100;
                            }
                            //await Task.Delay((int)(recastTimer.TotalMilliseconds - (recastTimer.TotalMilliseconds - TimeSpan.FromSeconds(EarlyCallout).TotalMilliseconds)), cts.Token);
                            PluginLog.Debug($"{Name} available again!");
                            CurrentStacks++;

                        } while (CurrentStacks != MaxStacks && !cts.IsCancellationRequested);
                    }
                    catch (TaskCanceledException)
                    {
                        PluginLog.Debug($"Task for {Name} cancelled");
                    }
                }, cts.Token);

                countdown.Start();
            }*/
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