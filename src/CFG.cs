using CounterStrikeSharp.API.Modules.Utils;
using System.Reflection;
using System.Text.Json;

internal class CFG
{
	public static Config config = new();

	public void CheckConfig(string moduleDirectory)
	{
		string path = Path.Join(moduleDirectory, "config.json");

		if (!File.Exists(path))
		{
			CreateAndWriteFile(path);
		}

		using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
		using (StreamReader sr = new StreamReader(fs))
		{
			// Deserialize the JSON from the file and load the configuration.
			config = JsonSerializer.Deserialize<Config>(sr.ReadToEnd())!;
		}

		if (config != null && config.ChatPrefix != null)
			config.ChatPrefix = ModifyColorValue(config.ChatPrefix);
	}

	private static void CreateAndWriteFile(string path)
	{

		using (FileStream fs = File.Create(path))
		{
			// File is created, and fs will automatically be disposed when the using block exits.
		}

		Console.WriteLine($"File created: {File.Exists(path)}");

		Config config = new Config
		{
			ChatPrefix = "{lightred}[PlayTime]",
			DatabaseHost = "localhost",
			DatabasePort = 3306,
			DatabaseUser = "root",
			DatabasePassword = "password",
			DatabaseName = "database",
		};

		// Serialize the config object to JSON and write it to the file.
		string jsonConfig = JsonSerializer.Serialize(config, new JsonSerializerOptions()
		{
			WriteIndented = true
		});
		File.WriteAllText(path, jsonConfig);
	}

	// Essential method for replacing chat colors from the config file, the method can be used for other things as well.
	private string ModifyColorValue(string msg)
	{
		if (msg.Contains('{'))
		{
			string modifiedValue = msg;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{{{field.Name}}}";
				if (msg.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}
			return modifiedValue;
		}

		return string.IsNullOrEmpty(msg) ? "[PlayTime]" : msg;
	}
}

internal class Config
{
	public string? ChatPrefix { get; set; }
	public string? DatabaseHost { get; set; }
	public int DatabasePort { get; set; }
	public string? DatabaseUser { get; set; }
	public string? DatabasePassword { get; set; }
	public string? DatabaseName { get; set; }
}