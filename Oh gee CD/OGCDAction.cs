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
        [JsonProperty]
        public byte RecastGroup { get; set; }
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
        public uint IconToDraw { get; set; } = 0;

        [JsonIgnore]
        public List<OGCDAbility> Abilities { get; set; } = new();
        [JsonIgnore]
        public TimeSpan Recast { get; set; }
        [JsonIgnore]
        public short MaxCharges { get; private set; }
        [JsonIgnore]
        public short CurrentCharges { get; private set; }
        [JsonIgnore]
        public double CooldownTimer { get; private set; }
        [JsonIgnore]
        public string AbilitiesNames => string.Join(" / ", Abilities.Select(a => a.Name));
        private uint currentJobLevel;
        CancellationTokenSource cts;

        public event EventHandler<SoundEventArgs>? SoundEvent;

        /// <summary>
        /// serialization constructor
        /// </summary>
        public OGCDAction()
        {
            TextToSpeechName = null!;
            cts = null!;
        }

        public OGCDAction(OGCDAbility ability, TimeSpan recast, byte recastGroup, uint currentJobLevel)
        {
            Abilities.Add(ability);
            Recast = recast;
            TextToSpeechName = ability.Name;
            RecastGroup = recastGroup;
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
        private int soundsToPlay = 1;

        public unsafe void StartCountdown(ActionManager* actionManager)
        {
            if (timerRunning)
            {
                return;
            }
            cts = new CancellationTokenSource();
            Task.Run(() =>
            {
                timerRunning = true;

                var recastGroupDetail = actionManager->GetRecastGroupDetail(RecastGroup);
                CurrentCharges = (short)Math.Floor(recastGroupDetail->Elapsed / Recast.TotalSeconds);
                if (CurrentCharges == MaxCharges || recastGroupDetail->IsActive != 1) return;

                soundsToPlay = 1;
                timerRunning = true;
                CooldownTimer = 0;
                bool resetEarlyCallout = true;
                PluginLog.Debug("Start:" + RecastGroup + "|" + CurrentCharges + "/" + MaxCharges);
                bool cancelled = false;
                do
                {
                    // reset early callout if the CooldownTimer is suddenly smaller than the new CooldownTimer 
                    var curTimeElapsed = recastGroupDetail->Elapsed;
                    var newCoolDown = ((Recast.TotalSeconds * MaxCharges - curTimeElapsed) % Recast.TotalSeconds);
                    resetEarlyCallout = resetEarlyCallout || CooldownTimer < newCoolDown;
                    CooldownTimer = newCoolDown;

                    var newCharges = (short)Math.Floor(recastGroupDetail->Elapsed / Recast.TotalSeconds);
                    if (newCharges < CurrentCharges)
                    {
                        soundsToPlay++;
                        PluginLog.Debug("UseCharge:" + RecastGroup + "|NewCharges:" + newCharges + "|CurrentCharges:" + CurrentCharges);
                    }

                    bool doIntermediateCallout = soundsToPlay > 1 && newCharges > CurrentCharges && EarlyCallout == 0.0;
                    bool doEarlyCallout = CooldownTimer <= EarlyCallout && soundsToPlay >= 1 && resetEarlyCallout;
                    if (doIntermediateCallout || doEarlyCallout)
                    {
                        PlaySound();
                        if (doEarlyCallout) resetEarlyCallout = false;
                        if (soundsToPlay > 0) soundsToPlay--;
                    }

                    CurrentCharges = newCharges;

                    Thread.Sleep(100);
                    if (cts.IsCancellationRequested)
                    {
                        PluginLog.Debug("Cancel:" + RecastGroup);
                        cancelled = true;
                    }
                    else
                    {
                        recastGroupDetail = actionManager->GetRecastGroupDetail(RecastGroup);
                    }

                } while (recastGroupDetail->IsActive == 1 && !cancelled && CurrentCharges != MaxCharges);

                CurrentCharges = MaxCharges;
                PluginLog.Debug("Ending:" + RecastGroup + "|" + "|Cancel:" + cancelled + "|Queue:" + soundsToPlay);
                if (soundsToPlay == 1 && !cancelled)
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
            cts?.Cancel();
        }

        public void MakeInactive()
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
        }

        public void MakeActive(uint currentJobLevel)
        {
            this.currentJobLevel = currentJobLevel;
            MaxCharges = (short)ActionManager.GetMaxCharges(Abilities[0].Id, currentJobLevel);
            CurrentCharges = MaxCharges;
            foreach (var ability in Abilities)
            {
                ability.CurrentJobLevel = currentJobLevel;
            }
            cts?.Cancel();
        }

        public void UpdateValuesFromOtherAction(OGCDAction fittingActionFromConfig)
        {
            DrawOnOGCDBar = fittingActionFromConfig.DrawOnOGCDBar;
            SoundEffect = fittingActionFromConfig.SoundEffect;
            SoundEffectEnabled = fittingActionFromConfig.SoundEffectEnabled;
            TextToSpeechName = fittingActionFromConfig.TextToSpeechName;
            TextToSpeechEnabled = fittingActionFromConfig.TextToSpeechEnabled;
            EarlyCallout = fittingActionFromConfig.EarlyCallout;
            SoundPath = fittingActionFromConfig.SoundPath;
            IconToDraw = fittingActionFromConfig.IconToDraw;
        }
    }
}