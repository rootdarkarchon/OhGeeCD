using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility.Signatures;
using Oh_gee_CD;
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
        public ChatGui ChatHandlers { get; }
        public DataManager DataManager { get; }
        private OhGeeCDConfiguration Configuration { get; init; }

        private PlayerManager playerManager;
        private SoundManager soundManager;
        private WindowSystem system;
        private SettingsUI ui;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            ClientState clientState, ChatGui chatHandlers, DataManager dataManager,
            Framework framework)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            ClientState = clientState;
            ChatHandlers = chatHandlers;
            DataManager = dataManager;
            soundManager = new SoundManager();
            playerManager = new PlayerManager(framework, dataManager, clientState, soundManager);
            Configuration = PluginInterface.GetPluginConfig() as OhGeeCDConfiguration ?? new OhGeeCDConfiguration(playerManager);
            Configuration.Initialize(PluginInterface);
            system = new WindowSystem("OhGeeCD");
            ui = new SettingsUI(playerManager, system, dataManager);
            soundManager.RegisterSoundSource(ui);

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
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            playerManager.Initialize(Configuration);
            Configuration.LoadedPlayerManager = playerManager;

            Configuration.Save();
        }

        public void Dispose()
        {
            Configuration.Save();
            soundManager.UnregisterSoundSource(ui);

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