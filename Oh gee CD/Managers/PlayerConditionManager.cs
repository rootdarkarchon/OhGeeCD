using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Newtonsoft.Json;
using System;

namespace OhGeeCD.Managers
{
    public class PlayerConditionManager : IDisposable
    {
        [Signature("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64", DetourName = nameof(ActorControlSelf_Detour))]
        private readonly Hook<ActorControlSelf>? actorControlSelfHook = null;

        private readonly Condition condition;
        private readonly ClientState clientState;


        // serialization constructor
        public PlayerConditionManager()
        {
            condition = null!;
            clientState = null!;
        }

        public PlayerConditionManager(Condition condition, ClientState clientState)
        {
            this.condition = condition;
            this.clientState = clientState;
            SignatureHelper.Initialise(this);

            actorControlSelfHook?.Enable();
        }

        private delegate void ActorControlSelf(uint entityId, uint id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);

        public event EventHandler<EventArgs>? WipeDetected;

        [JsonIgnore]
        public bool CutsceneActive => condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene78];

        [JsonProperty]
        public bool EnableAlways { get; set; } = false;

        [JsonProperty]
        public bool EnableInCombat { get; set; } = true;

        [JsonProperty]
        public bool EnableInDuty { get; set; } = false;

        [JsonIgnore]
        public bool InCombat => condition[ConditionFlag.InCombat];

        [JsonIgnore]
        public bool InDuty => condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95] || condition[ConditionFlag.BoundToDuty97];

        [JsonIgnore]
        public bool InPvP => clientState.IsPvP || condition[ConditionFlag.PvPDisplayActive] || condition[ConditionFlag.InDuelingArea];

        public void Dispose()
        {
            actorControlSelfHook?.Dispose();
        }

        public bool ProcessingActive()
        {
            bool active = false;
            active |= EnableAlways;
            active |= EnableInCombat && InCombat;
            active |= EnableInDuty && InDuty;
            active &= !CutsceneActive;
            active &= !InPvP;
            return active;
        }

        private void ActorControlSelf_Detour(uint entityId, uint id, uint arg0, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, ulong targetId, byte a10)
        {
            actorControlSelfHook?.Original(entityId, id, arg0, arg1, arg2, arg3, arg4, arg5, targetId, a10);
            if (arg1 == 0x40000010) // check for 'fade in' aka "wipe" and reset
            {
                WipeDetected?.Invoke(null, null!);
            }
        }
    }
}