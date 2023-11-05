using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Source2Framework.MySQL;

namespace K4ryuuPlaytimePlugin
{
	public class PlaytimePlugin : BasePlugin
	{
		MySqlDb? MySql = null;
		public override string ModuleName => "Playtime Tracker";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu";
		Dictionary<uint, Dictionary<string, DateTime>> clientTime = new Dictionary<uint, Dictionary<string, DateTime>>();

		public override void Load(bool hotReload)
		{
			new CFG().CheckConfig(ModuleDirectory);

			MySql = new MySqlDb(CFG.config.DatabaseHost!, CFG.config.DatabaseUser!, CFG.config.DatabasePassword!, CFG.config.DatabaseName!, CFG.config.DatabasePort);
			MySql.ExecuteNonQueryAsync(@"CREATE TABLE IF NOT EXISTS `player_stats` (`id` INT AUTO_INCREMENT PRIMARY KEY, `name` VARCHAR(255) NOT NULL, `steamid` VARCHAR(17) UNIQUE NOT NULL, `all` INT NOT NULL DEFAULT 0, `ct` INT NOT NULL DEFAULT 0, `t` INT NOT NULL DEFAULT 0, `spec` INT NOT NULL DEFAULT 0, `dead` INT NOT NULL DEFAULT 0, `alive` INT NOT NULL DEFAULT 0);");

			if (hotReload)
			{
				ForEachPlayer(InsertNewClient);
			}

			RegisterListener<Listeners.OnMapEnd>(() => ForEachPlayer(SaveClientTime));
		}

		[ConsoleCommand("playtime", "Check the current playtime")]
		[ConsoleCommand("time", "Check the current playtime")]
		[ConsoleCommand("mytime", "Check the current playtime")]
		public async void OnCommandCheckPlaytime(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || !player.IsValid)
				return;

			MySqlQueryResult result = await MySql!.Table("player_stats").Where($"steamid = '{player.SteamID}'").SelectAsync();

			if (result.Rows > 0)
			{
				Utilities.ReplyToCommand(player, $" {CFG.config.ChatPrefix} {ChatColors.LightRed}{player.PlayerName}{ChatColors.Gold}'s Playtime Statistics:");
				Utilities.ReplyToCommand(player, $" {ChatColors.Gold}Total: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "all"))}");
				Utilities.ReplyToCommand(player, $" {ChatColors.Gold}CT: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "ct"))}{ChatColors.Gold} | T: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "t"))}");
				Utilities.ReplyToCommand(player, $" {ChatColors.Gold}Spectator: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "spec"))}");
				Utilities.ReplyToCommand(player, $" {ChatColors.Gold}Alive: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "alive"))}{ChatColors.Gold} | Dead: {ChatColors.LightRed}{FormatPlaytime(result.Get<int>(0, "dead"))}");
			}
			else Utilities.ReplyToCommand(player, $" {CFG.config.ChatPrefix} {ChatColors.LightRed}We don't have your playtime data at the moment. Please check again later!");
		}

		[GameEventHandler]
		public HookResult OnClientConnect(EventPlayerConnectFull @event, GameEventInfo info)
		{
			CCSPlayerController player = @event.Userid;

			if (!player.IsValid || player.IsBot)
				return HookResult.Continue;

			InsertNewClient(player);

			uint playerIndex = player.PlayerPawn.Value.EntityIndex!.Value.Value;

			if (clientTime[playerIndex] == null)
				clientTime[playerIndex] = new Dictionary<string, DateTime>();

			clientTime[playerIndex]["Connect"] = DateTime.UtcNow;
			clientTime[playerIndex]["Team"] = DateTime.UtcNow;
			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnClientDisconnet(EventPlayerDisconnect @event, GameEventInfo info)
		{
			CCSPlayerController player = @event.Userid;

			if (!player.IsValid || player.IsBot)
				return HookResult.Continue;

			SaveClientTime(player);

			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnClientSpawn(EventPlayerSpawn @event, GameEventInfo info)
		{
			CCSPlayerController player = @event.Userid;
			if (!player.IsValid || player.IsBot) return HookResult.Continue;

			UpdatePlayerData(player, "dead", (DateTime.UtcNow - clientTime[player.PlayerPawn.Value.EntityIndex!.Value.Value]["Death"]).TotalSeconds);
			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnClientDeath(EventPlayerDeath @event, GameEventInfo info)
		{
			CCSPlayerController player = @event.Userid;
			if (!player.IsValid || player.IsBot) return HookResult.Continue;

			UpdatePlayerData(player, "alive", (DateTime.UtcNow - clientTime[player.PlayerPawn.Value.EntityIndex!.Value.Value]["Death"]).TotalSeconds);
			return HookResult.Continue;
		}

		[GameEventHandler]
		public HookResult OnClientTeam(EventPlayerTeam @event, GameEventInfo info)
		{
			CCSPlayerController player = @event.Userid;
			if (!player.IsValid || player.IsBot || @event.Oldteam == @event.Team) return HookResult.Continue;

			DateTime now = DateTime.UtcNow;
			uint playerIndex = player.PlayerPawn.Value.EntityIndex!.Value.Value;

			double seconds = (clientTime[playerIndex]["Team"] - now).TotalSeconds;

			UpdatePlayerData(player, GetFieldForTeam((CsTeam)@event.Oldteam), seconds);

			return HookResult.Continue;
		}

		public void InsertNewClient(CCSPlayerController player)
		{
			MySqlQueryValue values = new MySqlQueryValue()
									.Add("name", player.PlayerName)
									.Add("steamid", player.SteamID.ToString());

			MySql!.Table("player_stats").InsertIfNotExistAsync(values, $"`name` = '{player.PlayerName}'");
		}
		public void SaveClientTime(CCSPlayerController player)
		{
			DateTime now = DateTime.UtcNow;
			uint playerIndex = player.PlayerPawn.Value.EntityIndex!.Value.Value;

			MySqlQueryValue values = new MySqlQueryValue()
						.Add("all", $"`all` + {(clientTime[playerIndex]["Connect"] - now).TotalSeconds}");

			double teamSeconds = (clientTime[playerIndex]["Team"] - now).TotalSeconds;
			switch ((CsTeam)player.TeamNum)
			{
				case CsTeam.Terrorist:
					{
						values.Add("t", $"`t` + {teamSeconds}");
						break;
					}
				case CsTeam.CounterTerrorist:
					{
						values.Add("ct", $"`ct` + {teamSeconds}");
						break;
					}
				case CsTeam.Spectator:
					{
						values.Add("spec", $"`spec` + {teamSeconds}");
						break;
					}
			}

			string field = player.PawnIsAlive ? "alive" : "dead";
			values.Add(field, $"`{field}` + {(clientTime[playerIndex]["Death"] - now).TotalSeconds}");

			MySql!.Table("player_stats").Where($"steamid = '{player.SteamID}'").UpdateAsync(values);
		}

		private void UpdatePlayerData(CCSPlayerController player, string field, double value)
		{
			uint playerIndex = player.PlayerPawn.Value.EntityIndex!.Value.Value;
			DateTime now = DateTime.UtcNow;

			MySqlQueryValue values = new MySqlQueryValue().Add(field, $"`{field}` + {value}");

			MySql!.Table("player_stats").Where($"steamid = '{player.SteamID}'").UpdateAsync(values);
			clientTime[playerIndex][field] = now;
		}

		static string FormatPlaytime(int totalSeconds)
		{
			string[] units = { "y", "mo", "d", "h", "m", "s" };
			int[] values = { totalSeconds / 31536000, (totalSeconds % 31536000) / 2592000, (totalSeconds % 2592000) / 86400, (totalSeconds % 86400) / 3600, (totalSeconds % 3600) / 60, totalSeconds % 60 };

			StringBuilder formattedTime = new StringBuilder();

			for (int i = 0; i < units.Length; i++)
			{
				if (values[i] > 0)
				{
					formattedTime.Append($"{values[i]}{units[i]}, ");
				}
			}

			return formattedTime.ToString().TrimEnd(' ', ',');
		}

		private void ForEachPlayer(Action<CCSPlayerController> action)
		{
			for (int targetIndex = 1; targetIndex <= Server.MaxPlayers; targetIndex++)
			{
				CCSPlayerController targetController = new CCSPlayerController(NativeAPI.GetEntityFromIndex(targetIndex));
				if (!targetController.IsValid || targetController.IsBot) continue;
				action(targetController);
			}
		}

		private string GetFieldForTeam(CsTeam team)
		{
			switch (team)
			{
				case CsTeam.Terrorist:
					return "t";
				case CsTeam.CounterTerrorist:
					return "ct";
				default:
					return "spec";
			}
		}
	}
}
