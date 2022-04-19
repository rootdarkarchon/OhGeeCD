using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oh_gee_CD
{
    [Serializable]
    public class OGCDAction : IDisposable, ISoundSource
    {
        public List<OGCDAbility> Abilities { get; set; } = new();
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
        [JsonProperty]
        public byte RecastGroup { get; set; }

        [JsonIgnore]
        public TimeSpan Recast { get; set; }
        [JsonIgnore]
        public short MaxCharges { get; private set; }
        [JsonIgnore]
        public short CurrentCharges { get; private set; }
        [JsonIgnore]
        public bool IsCurrentClassJob { get; private set; }
        [JsonIgnore]
        public double CooldownTimer { get; private set; }
        private uint currentJobLevel;
        readonly CancellationTokenSource cts;

        public event EventHandler<SoundEventArgs>? SoundEvent;

        /// <summary>
        /// serialization constructor
        /// </summary>
        public OGCDAction()
        {
            TextToSpeechName = null!;
            cts = null!;
        }

        public OGCDAction(OGCDAbility ability, TimeSpan recast, byte cooldownGroup, uint currentJobLevel)
        {
            Abilities.Add(ability);
            Recast = recast;
            TextToSpeechName = ability.Name;
            RecastGroup = cooldownGroup;
            this.currentJobLevel = currentJobLevel;
            MaxCharges = (short)ActionManager.GetMaxCharges(ability.Id, currentJobLevel);
            CurrentCharges = MaxCharges;
            cts = new CancellationTokenSource();
        }

        public void Debug()
        {
            foreach (var ability in Abilities)
            {
                PluginLog.Debug($"Id:{ability.Id} | Name:{ability.Name} | MaxCharges:{MaxCharges} | CD:{Recast.TotalSeconds}s | ReqLevel:{ability.RequiredJobLevel} | CanCast:{ability.IsAvailable} | OverWritesOrOverwritten:{ability.OverwritesOrIsOverwritten}");
            }
        }

        public void SetTextToSpeechName(string newname)
        {
            TextToSpeechName = newname;
        }

        private bool timerRunning = false;
        private int soundQueue = 1;

        public unsafe void StartCountdown(ActionManager* actionManager, bool playSound = true)
        {
            if (timerRunning)
            {
                return;
            }
            Task.Run(() =>
            {
                soundQueue = playSound ? 1 : 0;
                timerRunning = true;
                CooldownTimer = 0;
                bool earlyCallOutReset = true;
                var detail = actionManager->GetRecastGroupDetail(RecastGroup);
                CurrentCharges = (short)Math.Floor(detail->Elapsed / Recast.TotalSeconds);

                do
                {
                    detail = actionManager->GetRecastGroupDetail(RecastGroup);

                    var curTimeElapsed = detail->Elapsed;

                    earlyCallOutReset = earlyCallOutReset || CooldownTimer < ((Recast.TotalSeconds * MaxCharges - curTimeElapsed) % Recast.TotalSeconds);
                    CooldownTimer = ((Recast.TotalSeconds * MaxCharges - curTimeElapsed) % Recast.TotalSeconds);

                    var stacks = (short)Math.Floor(detail->Elapsed / Recast.TotalSeconds);

                    if (stacks < CurrentCharges)
                    {
                        soundQueue++;
                    }

                    if (IsCurrentClassJob
                        && ((soundQueue > 1 && stacks > CurrentCharges && EarlyCallout == 0.0)
                           || (CooldownTimer <= EarlyCallout && soundQueue >= 1 && earlyCallOutReset)))
                    {
                        PlaySound();
                        if (CooldownTimer <= EarlyCallout && soundQueue >= 1 && earlyCallOutReset) earlyCallOutReset = false;
                        if (soundQueue > 0) soundQueue--;
                    }

                    CurrentCharges = stacks;
                    Thread.Sleep(100);

                } while (detail->IsActive == 1 && !cts.IsCancellationRequested && CurrentCharges != MaxCharges);

                CurrentCharges = MaxCharges;
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

        public void Dispose()
        {
            IsCurrentClassJob = false;
            cts?.Cancel();
        }

        public void MakeInactive()
        {
            IsCurrentClassJob = false;
        }

        public void MakeActive(uint currentJobLevel)
        {
            IsCurrentClassJob = true;
            this.currentJobLevel = currentJobLevel;
            MaxCharges = (short)ActionManager.GetMaxCharges(Abilities[0].Id, currentJobLevel);
            CurrentCharges = MaxCharges;
            foreach (var ability in Abilities)
            {
                ability.CurrentJobLevel = currentJobLevel;
            }
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