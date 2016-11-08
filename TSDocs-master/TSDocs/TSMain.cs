/*
 * Original plugin by Scavenger.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace TSDocs
{
    [ApiVersion(1, 25)]
    public class TSDocs : TerrariaPlugin
    {
        public static TSConfig getConfig { get; set; }
        public static string ConfigPath { get { return Path.Combine(TShock.SavePath, "TSDocs/Config.json"); } }
		public static string SavePath { get; set; }

        public override string Name
        {
            get { return "TSDocs"; }
        }

        public override string Author
        {
            get { return "Zaicon"; }
        }

        public override string Description
        {
            get { return "Powerful Documentation and MOTD Plugin. Show information from a file using a command that you define."; }
        }

        public override Version Version
        {
            get { return new Version("2.1.5"); }
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
            }
            base.Dispose(disposing);
        }

        public TSDocs(Main game) : base(game)
        {
            Order = 1;
			
			SavePath = Path.Combine(TShockAPI.TShock.SavePath, "TSDocs");
        }

        #region Initialize
        void OnInitialize(EventArgs e)
        {
			getConfig = new TSConfig();
			try
			{
				TSConfig.Re_LoadConfig();
				TSPlayer.Server.SendSuccessMessage("Loaded TSDocs Config!");
			}
			catch (Exception ex)
			{
				TSPlayer.Server.SendErrorMessage("Error: Config failed to reload, Check logs!");
				TShock.Log.Error("[TSDocs] Config Exception:");
				TShock.Log.Error(ex.ToString());
			}

			Commands.ChatCommands.Add(new Command("tsdocs.reload", TSConfig.CMDreload, "tsdocs"));
            Commands.ChatCommands.Add(new Command("tsdocs.news", CMDsetnews, "setnews", "snews"));
			Commands.ChatCommands.Add(new Command("tsdocs.news", CMDgetnews, "getnews", "gnews"));
        }
        #endregion

		#region CMD Get/Set News.txt
		private void CMDsetnews(CommandArgs args)
		{
			try
			{
				if (args.Parameters.Count < 2)
				{
					args.Player.SendErrorMessage("Usage: /snews <add / line number to edit> <new text>");
					return;
				}
				if (args.Parameters[0] == "add" || args.Parameters[0] == "a")
				{
					var path = Path.Combine(SavePath, getConfig.news_file);
					TSUtils.CheckFile(path);
					var NewsFile = File.ReadAllLines(path);

					var textParams = args.Parameters;
					textParams.RemoveAt(0);

					var NewNews = new List<string>();
					NewNews.Add(string.Join(" ", textParams));
					NewNews.AddRange(NewsFile);

					File.WriteAllLines(path, NewNews);

					args.Player.SendSuccessMessage("Sucessfuly added line to the news");
				}
				else
				{
					int line = 1;
					if (!int.TryParse(args.Parameters[0], out line))
					{
						args.Player.SendErrorMessage("Error: Line number is not an integer!");
						return;
					}
					if (line > getConfig.news_lines)
					{
                        args.Player.SendErrorMessage("Error: You can only set up to line {0}.", getConfig.news_lines.ToString());
						return;
					}

					var path = Path.Combine(SavePath, getConfig.news_file);
					TSUtils.CheckFile(path);
					var NewsFile = File.ReadAllLines(path);

					var textParams = args.Parameters;
					textParams.RemoveAt(0);

					NewsFile[line - 1] = string.Join(" ", textParams);
					File.WriteAllLines(path, NewsFile);
					args.Player.SendInfoMessage("Sucessfuly wrote line to the news");
				}
			}
			catch (Exception ex)
			{
				args.Player.SendErrorMessage("An error occoured while writing to the news file! Check the logs.", Color.IndianRed);
                TShock.Log.Error("[TSDocs] error while writing to the news file: \n" + ex.ToString());
			}

		}

		private void CMDgetnews(CommandArgs args)
		{
			try
			{
				int page = 1;
				if (args.Parameters.Count > 0)
					int.TryParse(args.Parameters[0], out page);
				page--;

				string path = Path.Combine(SavePath, getConfig.news_file);
				TSUtils.CheckFile(path);
				var NewsFile = File.ReadAllLines(path);

				Dictionary<string, Color> messages = new Dictionary<string, Color>();
				foreach (var n in NewsFile)
					messages.Add(n, Color.SeaGreen);

				var header = TSUtils.GetPaginationHeader("Get News", "/gnews", page + 1, (messages.Count / 6) + 1);

				TSUtils.Paginate(header, messages, page, args.Player);
			}
			catch (Exception ex)
			{
				args.Player.SendErrorMessage("An error occoured while reading the news file! Check the logs.", Color.IndianRed);
                TShock.Log.Error("[TSDocs] error while reading the news file: \n" + ex.ToString());
			}
		}
		#endregion

        #region Handle MOTD
        private void OnGreetPlayer(GreetPlayerEventArgs e)
        {
			if (getConfig.motd_enabled && TShock.Players[e.Who] != null)
            {
                ShowMOTD(TShock.Players[e.Who]);
            }
        }
        #endregion

        #region Show MOTD
		private void ShowMOTD(TSPlayer player)
        {
			try
			{
				string filetoshow = Path.Combine(SavePath, getConfig.motd.file);
				foreach (var group in getConfig.motd.groups)
				{
					if (group.Key == player.Group.Name)
					{
						filetoshow = Path.Combine(SavePath, group.Value);
					}
				}

				TSUtils.CheckFile(filetoshow);
				var file = File.ReadAllLines(filetoshow);

				var messages = TSUtils.ReplaceVariables(file, player);

				foreach (var msg in messages)
				{
					if (msg.Key.StartsWith("%command%") && msg.Key.EndsWith("%"))
					{
						string docmd = msg.Key.Split('%')[2];
						if (!docmd.StartsWith("/"))
							docmd = "/" + docmd;
						Commands.HandleCommand(player, docmd);
						continue;
					}
					else
						player.SendInfoMessage(msg.Key, msg.Value);
				}
			}
			catch (Exception ex)
			{
                TShock.Log.ConsoleError("Something when wrong when showing {0} a motd. Check the logs.".SFormat(player.Name));
                TShock.Log.Error(ex.ToString());
			}
        }
        #endregion

        #region Handle Command
        private void OnChat(ServerChatEventArgs e)
        {
            if (e.Handled || !e.Text.StartsWith("/"))
                return;

            foreach (var command in getConfig.commands)
            {
                if ((e.Text == command.command || e.Text.StartsWith(command.command + " ")) && command.file != "")
                {
                    e.Handled = true;
                    TShock.Log.Info("{0} executed: {1}".SFormat(TShock.Players[e.Who].Name, command.command));
                    ShowFile(command, e.Text, TShock.Players[e.Who]);
                    break;
                }
            }
        }
        #endregion

        #region Show Command
		public static void ShowFile(TSCommand command, string chat, TSPlayer player)
		{
			try
			{
				String filetoshow = Path.Combine(SavePath, command.file);
				foreach (var group in command.groups)
				{
					if (group.Key == player.Group.Name)
					{
						filetoshow = Path.Combine(SavePath, group.Value);
					}
				}

				Dictionary<string, Color> displayLines = new Dictionary<string, Color>();

				TSUtils.CheckFile(filetoshow);

				var file = File.ReadAllLines(filetoshow);

				var messages = TSUtils.ReplaceVariables(file, player);

				int page = 0;
				if (chat.Contains(" "))
				{
					var data = chat.Split(' ');
					if (int.TryParse(data[1], out page))
						page--;
					else
						player.SendErrorMessage(string.Format("Invalid page number ({0})", data[1]), Color.Red);
				}

				TSUtils.Paginate(TSUtils.GetPaginationHeader(command.name, command.command, page + 1, (messages.Count / 6) + 1),
								 messages, page, player);
			}
			catch (Exception ex)
			{
                TShock.Log.ConsoleError("Something when wrong when showing {0} \"{1}\". Check the Logs.".SFormat(player.Name, command.command));
                TShock.Log.Error(ex.ToString());
			}
		}
        #endregion
    }
}
