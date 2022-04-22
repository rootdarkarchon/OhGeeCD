using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using OhGeeCD.Sound;
using OhGeeCD.UI;
using System;

namespace OhGeeCD
{
    public sealed unsafe class Plugin : IDalamudPlugin
    {
        private const string commandName = "/pohgeecd";
        private readonly DrawHelper drawHelper;
        private readonly Framework framework;
        private readonly PlayerConditionManager playerConditionManager;
        private readonly WindowSystem system;
        private PlayerManager playerManager;
        private SettingsUI settingsUI;
        private SoundManager soundManager;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            ClientState clientState, DataManager dataManager,
            Framework framework, Condition condition)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            ClientState = clientState;
            DataManager = dataManager;
            this.framework = framework;
            drawHelper = new DrawHelper(dataManager);
            system = new WindowSystem("OhGeeCD");
            playerConditionManager = new PlayerConditionManager(condition);

            clientState.Login += State_Login;
            clientState.Logout += State_Logout;

            if (clientState.IsLoggedIn)
            {
                State_Login(null, EventArgs.Empty);
            }
        }

        public ClientState ClientState { get; }
        public DataManager DataManager { get; }
        public string Name => "Oh gee, CD";
        private CommandManager CommandManager { get; init; }
        private OhGeeCDConfiguration Configuration { get; set; }
        private DalamudPluginInterface PluginInterface { get; init; }

        public void Dispose()
        {
            Configuration.Save();

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

            CommandManager.RemoveHandler(commandName);
            playerManager.Dispose();
            settingsUI.Dispose();
            soundManager.Dispose();
        }

        private void DrawConfigUI()
        {
            settingsUI.Toggle();
        }

        private void DrawUI()
        {
            system.Draw();
        }

        private void OnCommand(string command, string args)
        {
            settingsUI.Toggle();
        }

        private void State_Login(object? sender, EventArgs e)
        {
            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand) { HelpMessage = "Opens Oh gee, CD configuration" });
            soundManager = new SoundManager(playerConditionManager);

            var dataLoader = new DataLoader(DataManager);

            playerManager = new PlayerManager(framework, dataLoader, ClientState, soundManager, system, drawHelper, playerConditionManager);
            settingsUI = new SettingsUI(playerManager, soundManager, playerConditionManager, system, drawHelper);

            Configuration = PluginInterface.GetPluginConfig() as OhGeeCDConfiguration ?? new OhGeeCDConfiguration(playerManager, soundManager, playerConditionManager);
            Configuration.Initialize(PluginInterface);

            Configuration.RestoreConfiguration(playerManager);
            Configuration.RestoreConfiguration(playerConditionManager);
            Configuration.RestoreConfiguration(soundManager);

            playerManager.Initialize();
            Configuration.DisposeAndUpdateWithNewEntities(playerManager, soundManager, playerConditionManager);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Configuration.Save();
        }

        private void State_Logout(object? sender, EventArgs e)
        {
            Dispose();
        }
    }
}