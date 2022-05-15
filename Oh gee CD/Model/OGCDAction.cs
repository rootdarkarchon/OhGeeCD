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
        private const double UPDATE_LOOP_MS = 50;

        /// <summary>
        /// serialization constructor
        /// </summary>
        public OGCDAction()
        {
            TextToSpeechName = null!;
            cts = null!;
        }

        public OGCDAction(OGCDAbility ability, byte recastGroup, uint currentJobLevel)
        {
            Abilities.Add(ability);
            TextToSpeechName = ability.Name;
            RecastGroup = recastGroup;
            this.currentJobLevel = currentJobLevel;
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
        public short MaxCurrentCharges { get; private set; }

        [JsonIgnore]
        public short MaxCharges { get; private set; }

        [JsonIgnore]
        public float Recast { get; private set; }

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
                PluginLog.Debug($"Id:{ability.Id} | Name:{ability.Name} | MaxCharges:{MaxCurrentCharges} | ReqLevel:{ability.RequiredJobLevel} | CanCast:{ability.IsAvailable} | OverWritesOrOverwritten:{ability.OverwritesOrIsOverwritten}");
            }
        }

        public void Dispose()
        {
            cts?.Cancel();
        }

        public unsafe void MakeActive(uint currentJobLevel)
        {
            this.currentJobLevel = currentJobLevel;
            MaxCharges = (short)ActionManager.GetMaxCharges(Abilities[0].Id, 90);
            MaxCurrentCharges = (short)ActionManager.GetMaxCharges(Abilities[0].Id, currentJobLevel);
            CurrentCharges = MaxCurrentCharges;
            foreach (var ability in Abilities)
            {
                ability.CurrentJobLevel = currentJobLevel;
            }
            cts?.Cancel();
        }

        public void MakeInactive()
        {
            cts?.Cancel();
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

                int soundsToPlay = 0;
                bool resetEarlyCallout = true;

                //MaxCharges = (short)ActionManager.GetMaxCharges(Abilities[0].Id, 90);
                //MaxCurrentCharges = (short)ActionManager.GetMaxCharges(Abilities[0].Id, currentJobLevel);

                Recast = recastGroupDetail->Total / MaxCharges;

                PluginLog.Debug("Start:" + RecastGroup + "|" + CurrentCharges + "/" + MaxCurrentCharges + "/" + MaxCharges + "|" + Recast + ":" + recastGroupDetail->Total + ":" + recastGroupDetail->Elapsed);
                do
                {
                    var newCoolDown = (Recast * MaxCurrentCharges - recastGroupDetail->Elapsed) % Recast;
                    resetEarlyCallout |= CooldownTimer < newCoolDown;
                    CooldownTimer = newCoolDown;

                    var newCharges = (short)Math.Floor(recastGroupDetail->Elapsed / Recast);
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
                        if (soundsToPlay == 0)
                        {
                            cts.Cancel();
                        }
                    }

                    Thread.Sleep((int)UPDATE_LOOP_MS);
                    recastGroupDetail = actionManager->GetRecastGroupDetail(RecastGroup);
                } while (recastGroupDetail->IsActive == 1 && !cts.IsCancellationRequested && CurrentCharges != MaxCurrentCharges);

                // loop remaining cooldowntimer down
                while (CooldownTimer > 0)
                {
                    CooldownTimer -= UPDATE_LOOP_MS / 1000;
                    Thread.Sleep((int)UPDATE_LOOP_MS);
                }

                CurrentCharges = MaxCurrentCharges;
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