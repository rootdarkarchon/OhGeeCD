using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;

namespace OhGeeCD
{
    public unsafe sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Oh gee, CD";

        private const string commandName = "/pohgeecd";

        private Framework Framework { init; get; } 

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public ClientState ClientState { get; }
        public ChatGui ChatHandlers { get; }
        public DataManager DataManager { get; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }

        private PlayerManager playerManager;

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
            Framework = framework;
            playerManager = new PlayerManager(framework, dataManager, clientState);
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
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

            playerManager.Initialize();
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