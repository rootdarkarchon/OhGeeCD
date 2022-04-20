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
        public uint IconToDraw { get; set; } = 0;

        [JsonIgnore]
        public TimeSpan Recast { get; set; }
        [JsonIgnore]
        public short MaxCharges { get; private set; }
        [JsonIgnore]
        public short CurrentCharges { get; private set; }
        [JsonIgnore]
        public double CooldownTimer { get; private set; }
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
        private int soundQueue = 1;

        public unsafe void StartCountdown(ActionManager* actionManager)
        {
            if (timerRunning)
            {
                //PluginLog.Debug("Running:" + RecastGroup + ":" + CurrentCharges + "/" + MaxCharges);
                return;
            }
            cts = new CancellationTokenSource();
            Task.Run(() =>
            {
                timerRunning = true;
                // wait and see if it actually needs to run, ffxiv has the tendency to somehow 
                // just set the ability to active just to make it inactive just a second or so later
                Thread.Sleep(1000);

                var detail = actionManager->GetRecastGroupDetail(RecastGroup);
                CurrentCharges = (short)Math.Floor(detail->Elapsed / Recast.TotalSeconds);
                if (CurrentCharges == MaxCharges || detail->IsActive != 1) return;

                soundQueue = 1;
                timerRunning = true;
                CooldownTimer = 0;
                bool earlyCallOutReset = true;
                PluginLog.Debug("Starting:" + RecastGroup + ":" + CurrentCharges + "/" + MaxCharges);
                bool cancelled = false;
                do
                {
                    detail = actionManager->GetRecastGroupDetail(RecastGroup);

                    var curTimeElapsed = detail->Elapsed;

                    // reset early callout if the CooldownTimer is suddenly smaller than the new CooldownTimer 
                    var newCoolDown = ((Recast.TotalSeconds * MaxCharges - curTimeElapsed) % Recast.TotalSeconds);
                    earlyCallOutReset = earlyCallOutReset || CooldownTimer < newCoolDown;
                    //PluginLog.Debug("CalloutReset:" + earlyCallOutReset);
                    CooldownTimer = newCoolDown;

                    var newCharges = (short)Math.Floor(detail->Elapsed / Recast.TotalSeconds);

                    if (newCharges < CurrentCharges)
                    {
                        soundQueue++;
                        PluginLog.Debug(RecastGroup + ":NewCharges|" + newCharges + ":CurrentCharges|" + CurrentCharges);
                    }

                    if ((soundQueue > 1 && newCharges > CurrentCharges && EarlyCallout == 0.0) // if we have more charges than we had in the prior loop, we need to notify
                           || (CooldownTimer <= EarlyCallout && soundQueue >= 1 && earlyCallOutReset)) // if the timer is below early callout and we have a sound queue of greater equal 1 we also need to notify
                    {
                        PlaySound();
                        if (CooldownTimer <= EarlyCallout && soundQueue >= 1 && earlyCallOutReset) earlyCallOutReset = false;
                        if (soundQueue > 0) soundQueue--;
                    }

                    CurrentCharges = newCharges;

                    Thread.Sleep(100);
                    if (cts.IsCancellationRequested)
                    {
                        PluginLog.Debug("Cancel:" + RecastGroup);
                        cancelled = true;
                    }

                } while (detail->IsActive == 1 && !cancelled && CurrentCharges != MaxCharges);

                CurrentCharges = MaxCharges;
                PluginLog.Debug("Ending:" + RecastGroup + ":" + CurrentCharges + "/" + MaxCharges + ":Cancel|" + cancelled + "|Queue:" + soundQueue);
                if (soundQueue == 1 && !cancelled)
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
            OGCDBarId = fittingActionFromConfig.OGCDBarId;
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