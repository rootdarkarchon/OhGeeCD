using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;

namespace Oh_gee_CD
{
    public unsafe sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Oh gee, CD";

        private const string commandName = "/pohgeecd";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public ClientState ClientState { get; }
        public DataManager DataManager { get; }
        private OhGeeCDConfiguration Configuration { get; init; }

        private readonly PlayerManager playerManager;
        private readonly SoundManager soundManager;
        private readonly WindowSystem system;
        private readonly DrawHelper drawHelper;
        private readonly SettingsUI ui;

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
            soundManager = new SoundManager();
            drawHelper = new DrawHelper(dataManager);
            system = new WindowSystem("OhGeeCD");

            playerManager = new PlayerManager(framework, dataManager, clientState, soundManager, system, drawHelper, condition);
            Configuration = PluginInterface.GetPluginConfig() as OhGeeCDConfiguration ?? new OhGeeCDConfiguration(playerManager);
            Configuration.Initialize(PluginInterface);
            ui = new SettingsUI(playerManager, system, drawHelper);

            commandManager.AddHandler(commandName, new CommandInfo(OnCommand));

            if (!clientState.IsLoggedIn)
            {
                clientState.Login += State_Login;
            }
            else
            {
                State_Login(null, EventArgs.Empty);
            }
        }

        private void State_Login(object? sender, EventArgs e)
        {
            playerManager.Initialize(Configuration);
            Configuration.LoadedPlayerManager.Dispose();
            Configuration.LoadedPlayerManager = playerManager;

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            Configuration.Save();
        }

        public void Dispose()
        {
            Configuration.Save();
            soundManager.UnregisterSoundSource(ui);

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            CommandManager.RemoveHandler(commandName);
            playerManager.Dispose();
            ui.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            ui.Toggle();
        }

        private void DrawUI()
        {
            system.Draw();
        }

        private void DrawConfigUI()
        {
            ui.Toggle();
        }
    }
}