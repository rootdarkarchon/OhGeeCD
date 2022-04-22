using Dalamud.Game.ClientState.Conditions;
using Newtonsoft.Json;

namespace OhGeeCD
{
    public class PlayerConditionManager
    {
        private readonly Condition condition;

        public PlayerConditionManager(Condition condition)
        {
            this.condition = condition;
        }

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

        public bool ProcessingActive()
        {
            bool show = false;
            show |= EnableAlways;
            show |= EnableInCombat && InCombat;
            show |= EnableInDuty && InDuty;
            show &= !CutsceneActive;
            return show;
        }
    }
}