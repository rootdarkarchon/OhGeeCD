using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;
using OhGeeCD.Interfaces;
using OhGeeCD.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OhGeeCD.Model
{
    [Serializable]
    public class OGCDAction : IDisposable, ISoundSource
    {
        private Task? CountdownTask;
        private CancellationTokenSource cts;
        private uint currentJobLevel;
        private double earlyCallout = 0.5;

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

        public event EventHandler<SoundEventArgs>? SoundEvent;

        [JsonIgnore]
        public List<OGCDAbility> Abilities { get; set; } = new();

        [JsonIgnore]
        public string AbilitiesNames => string.Join(" / ", Abilities.Select(a => a.Name));

        [JsonIgnore]
        public double CooldownTimer { get; private set; }

        [JsonIgnore]
        public short CurrentCharges { get; private set; }

        [JsonProperty]
        public double EarlyCallout
        {
            get => earlyCallout;
            set
            {
                earlyCallout = value <= 0.0 ? 0.1 : value;
            }
        }

        [JsonProperty]
        public uint IconToDraw { get; set; } = 0;

        [JsonIgnore]
        public short MaxCharges { get; private set; }

        [JsonIgnore]
        public TimeSpan Recast { get; set; }

        [JsonProperty]
        public byte RecastGroup { get; set; }

        [JsonProperty]
        public int SoundEffect { get; set; }

        [JsonProperty]
        public bool SoundEffectEnabled { get; set; } = false;

        [JsonProperty]
        public string SoundPath { get; set; } = string.Empty;

        [JsonProperty]
        public bool TextToSpeechEnabled { get; set; } = false;

        [JsonProperty]
        public string TextToSpeechName { get; set; }

        [JsonProperty]
        public bool Visualize { get; set; } = false;

        public void Debug()
        {
            foreach (var ability in Abilities)
            {
                PluginLog.Debug($"Id:{ability.Id} | Name:{ability.Name} | MaxCharges:{MaxCharges} | CD:{Recast.TotalSeconds}s | ReqLevel:{ability.RequiredJobLevel} | CanCast:{ability.IsAvailable} | OverWritesOrOverwritten:{ability.OverwritesOrIsOverwritten}");
            }
        }

        public void Dispose()
        {
            cts?.Cancel();
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

        public void MakeInactive()
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
        }

        public void SetTextToSpeechName(string newname)
        {
            TextToSpeechName = newname;
        }

        public unsafe void StartCountdown(ActionManager* actionManager)
        {
            if (!CountdownTask?.IsCompleted ?? false)
            {
                return;
            }
            cts = new CancellationTokenSource();
            CountdownTask = Task.Run(() =>
            {
                var recastGroupDetail = actionManager->GetRecastGroupDetail(RecastGroup);
                //CurrentCharges = (short)Math.Floor(recastGroupDetail->Elapsed / Recast.TotalSeconds);
                //if (CurrentCharges == MaxCharges || recastGroupDetail->IsActive != 1) return;

                int soundsToPlay = 0;
                bool resetEarlyCallout = true;
                PluginLog.Debug("Start:" + RecastGroup + "|" + CurrentCharges + "/" + MaxCharges);
                do
                {
                    var newCoolDown = (Recast.TotalSeconds * MaxCharges - recastGroupDetail->Elapsed) % Recast.TotalSeconds;
                    resetEarlyCallout = resetEarlyCallout || CooldownTimer < newCoolDown;
                    CooldownTimer = newCoolDown;

                    var newCharges = (short)Math.Floor(recastGroupDetail->Elapsed / Recast.TotalSeconds);
                    if (newCharges < CurrentCharges)
                    {
                        soundsToPlay++;
                        PluginLog.Debug("UseCharge:" + RecastGroup + "|NewCharges:" + newCharges + "|CurrentCharges:" + CurrentCharges);
                    }
                    CurrentCharges = newCharges;

                    bool doEarlyCallout = CooldownTimer <= EarlyCallout && soundsToPlay > 0 && resetEarlyCallout;
                    if (doEarlyCallout)
                    {
                        PlaySound();
                        resetEarlyCallout = false;
                        soundsToPlay--;
                    }

                    Thread.Sleep(100);
                    if (cts.IsCancellationRequested)
                    {
                        PluginLog.Debug("Cancel:" + RecastGroup);
                    }
                    else
                    {
                        recastGroupDetail = actionManager->GetRecastGroupDetail(RecastGroup);
                    }
                } while (recastGroupDetail->IsActive == 1 && !cts.IsCancellationRequested && CurrentCharges != MaxCharges);

                CurrentCharges = MaxCharges;
                CooldownTimer = 0;

                PluginLog.Debug("Ended:" + RecastGroup + "|" + "|Cancel:" + cts.IsCancellationRequested + "|Queue:" + soundsToPlay);
            }, cts.Token);
        }

        public void UpdateValuesFromOtherAction(OGCDAction fittingActionFromConfig)
        {
            Visualize = fittingActionFromConfig.Visualize;
            SoundEffect = fittingActionFromConfig.SoundEffect;
            SoundEffectEnabled = fittingActionFromConfig.SoundEffectEnabled;
            TextToSpeechName = fittingActionFromConfig.TextToSpeechName;
            TextToSpeechEnabled = fittingActionFromConfig.TextToSpeechEnabled;
            EarlyCallout = fittingActionFromConfig.EarlyCallout;
            SoundPath = fittingActionFromConfig.SoundPath;
            IconToDraw = fittingActionFromConfig.IconToDraw;
        }

        private unsafe void PlaySound()
        {
            SoundEvent?.Invoke(this, new SoundEventArgs(TextToSpeechEnabled ? TextToSpeechName : null,
                SoundEffectEnabled ? SoundEffect : null,
                SoundEffectEnabled ? SoundPath : null));
        }
    }
}