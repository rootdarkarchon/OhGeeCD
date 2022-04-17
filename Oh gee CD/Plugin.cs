using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
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
        private PluginUI PluginUi { get; init; }

        private PlayerManager playerManager;
        private SoundManager soundManager;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            ClientState clientState, ChatGui chatHandlers, DataManager dataManager,
            Framework framework, SigScanner sigScanner)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            ClientState = clientState;
            ChatHandlers = chatHandlers;
            DataManager = dataManager;
            soundManager = new SoundManager(sigScanner);
            playerManager = new PlayerManager(framework, dataManager, clientState, soundManager);
            Configuration = PluginInterface.GetPluginConfig() as OhGeeCDConfiguration ?? new OhGeeCDConfiguration(playerManager);
            Configuration.Initialize(PluginInterface);

            PluginUi = new PluginUI(Configuration);

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
            playerManager.Dispose();
            PluginUi.Dispose();
            CommandManager.RemoveHandler(commandName);
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            //PluginUi.Visible = true;

        }

        private void DrawUI()
        {
            PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            PluginUi.SettingsVisible = true;
        }
    }
}