﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace AntiSpam
{
	[ApiVersion(1, 16)]
	public class AntiSpam : TerrariaPlugin
	{
		Config Config = new Config();
		DateTime[] Times = new DateTime[256];
		double[] Spams = new double[256];

		public override string Author
		{
			get { return "MarioE"; }
		}
		public override string Description
		{
			get { return "Prevents spamming."; }
		}
		public override string Name
		{
			get { return "AntiSpam"; }
		}
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public AntiSpam(Main game)
			: base(game)
		{
			Order = 1000000;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
				ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
			}
		}
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetSendData.Register(this, OnSendData);
			ServerApi.Hooks.ServerChat.Register(this, OnChat);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
		}

		void OnChat(ServerChatEventArgs e)
		{
			if (!e.Handled)
			{
				string text = e.Text;
				if (e.Text.StartsWith("/"))
				{
					string[] arr = e.Text.Split(' ');
					if (e.Text.StartsWith("/me ") && TShock.Players[e.Who].Group.HasPermission(Permissions.cantalkinthird))
						text = e.Text.Substring(4);
					else if ((e.Text.StartsWith("/tell ") || e.Text.StartsWith("/w ") || e.Text.StartsWith("/whisper ")) &&
						TShock.Players[e.Who].Group.HasPermission(Permissions.whisper) && (arr.Length > 1 && !String.IsNullOrWhiteSpace(arr[1])))
					{
						text = e.Text.Substring(arr[0].Length + arr[1].Length + 2);
					}
					else if ((e.Text.StartsWith("/r ") || e.Text.StartsWith("/reply ")) &&
						TShock.Players[e.Who].Group.HasPermission(Permissions.whisper))
					{
						text = e.Text.Substring(arr[0].Length + 1);
					}
					else if (e.Text.Trim().Length == 1)
						text = "/";
                    else
                        return;
				}
				if ((DateTime.Now - Times[e.Who]).TotalSeconds > Config.Time)
				{
					Spams[e.Who] = 0.0;
					Times[e.Who] = DateTime.Now;
				}

				Spams[e.Who]++;
				double uniqueRatio = (double)text.GetUnique() / text.Length;
				if (text.Trim().Length <= Config.ShortLength)
					Spams[e.Who] += 0.5;
				else if (uniqueRatio <= 0.20 || uniqueRatio >= 0.80)
					Spams[e.Who] += 0.5;
				if (text.UpperCount() >= Config.CapsRatio)
					Spams[e.Who] += 0.5;

				if (Spams[e.Who] > Config.Threshold && !TShock.Players[e.Who].Group.HasPermission("antispam.ignore"))
				{
					switch (Config.Action)
					{
						case "ignore":
						default:
							Times[e.Who] = DateTime.Now;
							TShock.Players[e.Who].SendErrorMessage("You have been ignored for spamming.");
							e.Handled = true;
							break;
						case "kick":
							TShock.Utils.ForceKick(TShock.Players[e.Who], "Spamming");
							e.Handled = true;
							break;
					}
				}
			}
		}
		void OnInitialize(EventArgs e)
		{
			Commands.ChatCommands.Add(new Command("antispam.reload", Reload, "asreload"));

			string path = Path.Combine(TShock.SavePath, "antispamconfig.json");
			if (File.Exists(path))
				Config = Config.Read(path);
			Config.Write(path);
		}
		void OnLeave(LeaveEventArgs e)
		{
			Spams[e.Who] = 0.0;
			Times[e.Who] = DateTime.Now;
		}
		void OnSendData(SendDataEventArgs e)
		{
			if (e.MsgId == PacketTypes.ChatText && !e.Handled)
			{
				if (Config.DisableBossMessages && e.number2 == 175 && e.number3 == 75 && e.number4 == 255)
				{
					if (e.text.StartsWith("Eye of Cthulhu") || e.text.StartsWith("Eater of Worlds") ||
						e.text.StartsWith("Skeletron") || e.text.StartsWith("King Slime") ||
						e.text.StartsWith("The Destroyer") || e.text.StartsWith("The Twins") ||
						e.text.StartsWith("Skeletron Prime") || e.text.StartsWith("Wall of Flesh") ||
						e.text.StartsWith("Plantera") || e.text.StartsWith("Golem") || e.text.StartsWith("Brain of Cthulhu") ||
						e.text.StartsWith("Queen Bee") || e.text.StartsWith("Duke Fishron"))
					{
						e.Handled = true;
					}
				}
				if (Config.DisableOrbMessages && e.number2 == 50 && e.number3 == 255 && e.number4 == 130)
				{
					if (e.text == "A horrible chill goes down your spine..." ||
						e.text == "Screams echo around you...")
					{
						e.Handled = true;
					}
				}
			}
		}

		void Reload(CommandArgs e)
		{
			string path = Path.Combine(TShock.SavePath, "antispamconfig.json");
			if (File.Exists(path))
				Config = Config.Read(path);
			Config.Write(path);
			e.Player.SendSuccessMessage("Reloaded antispam config.");
		}
	}
}