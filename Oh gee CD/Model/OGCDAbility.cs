using Newtonsoft.Json;

namespace OhGeeCD.Model
{
    public class OGCDAbility
    {
        public OGCDAbility(uint id, uint icon, string name, byte requiredJobLevel, uint jobLevel, bool isRoleAction)
        {
            Id = id;
            Icon = icon;
            Name = name;
            RequiredJobLevel = requiredJobLevel;
            IsRoleAction = isRoleAction;
            CurrentJobLevel = jobLevel;
        }

        [JsonIgnore]
        public uint CurrentJobLevel { get; set; }

        [JsonIgnore]
        public uint Icon { get; set; }

        [JsonIgnore]
        public uint Id { get; set; }

        [JsonIgnore]
        public bool IsAvailable => CurrentJobLevel >= RequiredJobLevel && (OtherAbility?.RequiredJobLevel ?? 0) <= RequiredJobLevel
            || CurrentJobLevel >= RequiredJobLevel && (OtherAbility?.RequiredJobLevel ?? 90) > CurrentJobLevel;

        [JsonIgnore]
        public bool IsRoleAction { get; set; }

        [JsonIgnore]
        public string Name { get; set; }

        [JsonIgnore]
        public OGCDAbility? OtherAbility { get; set; }

        [JsonIgnore]
        public bool OverwritesOrIsOverwritten => Id != (OtherAbility?.Id ?? Id);

        [JsonIgnore]
        public byte RequiredJobLevel { get; set; }

        public override string ToString()
        {
            return $"{Id}|{Name}|{RequiredJobLevel}|{OtherAbility?.Id}|{IsAvailable}|{IsRoleAction}";
        }
    }
}