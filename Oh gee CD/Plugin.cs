using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using OhGeeCD.Managers;
using OhGeeCD.UI;
using OhGeeCD.Util;
using System;

namespace OhGeeCD
{
    public sealed unsafe class Plugin : IDalamudPlugin
    {
        private const string commandName = "/pohgeecd";
        private readonly ClientState clientState;
        private readonly CommandManager commandManager;
        private readonly Condition condition;
        private readonly DataManager dataManager;
        private readonly DrawHelper drawHelper;
        private readonly Framework framework;
        private readonly DalamudPluginInterface pluginInterface;
        private readonly WindowSystem windowSystem;
        private OhGeeCDConfiguration? configuration;
        private OGCDTrackerUI? ogcdTracker;
        private PlayerConditionManager? playerConditionManager;
        private PlayerManager? playerManager;
        private SettingsUI? settingsUI;
        private SoundManager? soundManager;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            ClientState clientState, DataManager dataManager,
            Framework framework, Condition condition)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.clientState = clientState;
            this.dataManager = dataManager;
            this.framework = framework;
            this.condition = condition;
            drawHelper = new DrawHelper(dataManager);
            windowSystem = new WindowSystem("OhGeeCD");

            clientState.Login += State_Login;
            clientState.Logout += State_Logout;

            if (clientState.IsLoggedIn)
            {
                InitializePlugin();
            }
        }

        public string Name => "Oh gee, CD";

        public void Dispose()
        {
            configuration?.Save();

            pluginInterface.UiBuilder.Draw -= DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

            commandManager?.RemoveHandler(commandName);
            playerManager?.Dispose();
            settingsUI?.Dispose();
            soundManager?.Dispose();
            playerConditionManager?.Dispose();
            ogcdTracker?.Dispose();
        }

        private void DrawConfigUI()
        {
            settingsUI?.Toggle();
        }

        private void DrawUI()
        {
            windowSystem.Draw();
        }

        private void OnCommand(string command, string args)
        {
            settingsUI?.Toggle();
        }

        private void State_Login(object? sender, EventArgs e)
        {
            InitializePlugin();
        }

        private void InitializePlugin()
        {
            commandManager.AddHandler(commandName, new CommandInfo(OnCommand) { HelpMessage = "Opens Oh gee, CD configuration" });
            var dataLoader = new DataLoader(dataManager);
            playerConditionManager = new PlayerConditionManager(condition, clientState, dataLoader.GetPvPTerritoryTypes());
            soundManager = new SoundManager(playerConditionManager);

            playerManager = new PlayerManager(framework, dataLoader, clientState, soundManager, windowSystem, drawHelper, playerConditionManager);
            settingsUI = new SettingsUI(playerManager, soundManager, playerConditionManager, windowSystem, drawHelper);
            ogcdTracker = new OGCDTrackerUI(windowSystem, playerManager, playerConditionManager, drawHelper);

            playerManager.Initialize();

            configuration = pluginInterface.GetPluginConfig() as OhGeeCDConfiguration ?? new OhGeeCDConfiguration(playerManager, soundManager, playerConditionManager);
            configuration.Initialize(pluginInterface);

            configuration.RestoreConfiguration(playerManager);
            configuration.RestoreConfiguration(playerConditionManager);
            configuration.RestoreConfiguration(soundManager);
            if (configuration.PlayerManager != playerManager)
            {
                configuration.DisposeAndUpdateWithNewEntities(playerManager, soundManager, playerConditionManager);
            }

            pluginInterface.UiBuilder.Draw += DrawUI;
            pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            configuration.Save();
        }

        private void State_Logout(object? sender, EventArgs e)
        {
            Dispose();
        }
    }
}