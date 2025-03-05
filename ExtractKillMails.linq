<Query Kind="Statements">
  <Namespace>System.Text.Json.Serialization</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>EveFrontier</Namespace>
  <Namespace>System.Globalization</Namespace>
</Query>

var killmails = Directory.GetFiles(@"C:\source\killmails\", "*.txt");
var killmailsApi = File.ReadAllText(@"C:\source\killmails\api.json");
var apiData = JsonSerializer.Deserialize<IEnumerable<ApiKillReport>>(killmailsApi);
var carbonData = new List<KillReport>();

foreach (var killmailFile in killmails) {
	var carbonKillmailText = File.ReadAllText(killmailFile);

	try {
		var carbonKillmail = KillReportParser.FromText(carbonKillmailText);
		carbonKillmail.ChainKill = KillReportValidator.Validate(apiData, carbonKillmail);
		carbonData.Add(carbonKillmail);
	} catch (Exception ex) {
		killmailFile.Dump();
		carbonKillmailText.Dump(ex.Message);
	}
}

carbonData.GroupBy(x => x.ChainKill.Count()).Dump();

namespace EveFrontier {
  public class KillReportValidator {
		public static IEnumerable<ApiKillReport> Validate(IEnumerable<ApiKillReport> chainData, KillReport candidate) {
			var killer = candidate.InvolvedParties.Single(x => x.LaidFinalBlow);
			var matchedParties = chainData.Where(x => x.Killer.Name == killer.Name && x.Victim.Name == candidate.Victim.Name);
			
			if (matchedParties.Count() == 1) {
				return matchedParties;
			}
			
			var matchedTimes = matchedParties.Where(x => x.Timestamp == candidate.Timestamp);
			
			if (matchedTimes.Count() == 1) {
				return matchedTimes;
			}
			
			return matchedTimes;
		}
	}

	public class ApiKillReport {
		[JsonPropertyName("victim")]
		public ApiPlayer Victim { get; set; }

		[JsonPropertyName("killer")]
		public ApiPlayer Killer { get; set; }

		[JsonPropertyName("solar_system_id")]
		public int SolarSystemId { get; set; }

		[JsonPropertyName("loss_type")]
		public string LossType { get; set; }

		[JsonPropertyName("timestamp")]
		[JsonConverter(typeof(LdapTimestampConverter))]
		public DateTime Timestamp { get; set; }

		public static ApiKillReport FromJson(string json) {
			return JsonSerializer.Deserialize<ApiKillReport>(json);
		}
	}

	public class ApiPlayer {
		[JsonPropertyName("address")]
		public string Address { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }
	}

	public class LdapTimestampConverter : JsonConverter<DateTime> {
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			long ldapTimestamp = reader.GetInt64();
			return DateTimeOffset.FromFileTime(ldapTimestamp).UtcDateTime;
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) {
			long ldapTimestamp = value.ToUniversalTime().ToFileTimeUtc();
			writer.WriteNumberValue(ldapTimestamp);
		}
	}

	public class KillReport {
		public DateTime Timestamp { get; set; }
		public Player Victim { get; set; }
		public string System { get; set; }
		public string Security { get; set; }
		public int DamageTaken { get; set; }
		public List<InvolvedParty> InvolvedParties { get; set; } = new();
		public List<Item> DestroyedItems { get; set; } = new();
		public List<Item> DroppedItems { get; set; } = new();
		public IEnumerable<ApiKillReport> ChainKill { get; set; } = null;
	}

	public class KillReportParser {
		public static KillReport FromText(string text) {
			var report = new KillReport();
			var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

			report.Timestamp = DateTime.ParseExact(lines[0].Trim(), "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
			report.Victim = new Player { Name = ExtractValue(lines, "Victim:") };
			report.Victim.Corp = ExtractValue(lines, "Corp:");
			report.Victim.Alliance = ExtractValue(lines, "Alliance:");
			report.Victim.Faction = ExtractValue(lines, "Faction:");
			report.Victim.Destroyed = ExtractValue(lines, "Destroyed:");
			report.System = ExtractValue(lines, "System:");
			report.Security = ExtractValue(lines, "Security:");
			report.DamageTaken = int.Parse(ExtractValue(lines, "Damage Taken:"));

			int index = Array.IndexOf(lines, "Involved parties:");
			for (int i = index + 1; i < lines.Length && !lines[i].Contains("Destroyed items:"); i++) {
				if (!lines[i].StartsWith("Name:")) continue;

				var nameValue = ExtractValue(lines, i, "Name:");
				bool finalBlow = nameValue.Contains("(laid the final blow)");
				nameValue = nameValue.Replace("(laid the final blow)", "").Trim();

				var involved = new InvolvedParty { Name = nameValue, LaidFinalBlow = finalBlow };

				if (i + 1 < lines.Length && lines[i + 1].StartsWith("Security:"))
					involved.Security = ExtractValue(lines, i + 1, "Security:");

				if (i + 2 < lines.Length && lines[i + 2].StartsWith("Corp:"))
					involved.Corp = ExtractValue(lines, i + 2, "Corp:");

				if (i + 3 < lines.Length && lines[i + 3].StartsWith("Alliance:"))
					involved.Alliance = ExtractValue(lines, i + 3, "Alliance:");

				if (i + 4 < lines.Length && lines[i + 4].StartsWith("Faction:"))
					involved.Faction = ExtractValue(lines, i + 4, "Faction:");

				if (i + 5 < lines.Length && lines[i + 5].StartsWith("Ship:"))
					involved.Ship = ExtractValue(lines, i + 5, "Ship:");

				if (i + 6 < lines.Length && lines[i + 6].StartsWith("Weapon:"))
					involved.Weapon = ExtractValue(lines, i + 6, "Weapon:");

				if (i + 7 < lines.Length && lines[i + 7].StartsWith("Damage Done:"))
					involved.DamageDone = int.Parse(ExtractValue(lines, i + 7, "Damage Done:"));

				report.InvolvedParties.Add(involved);
			}

			report.DestroyedItems = ExtractItems(lines, "Destroyed items:");
			report.DroppedItems = ExtractItems(lines, "Dropped items:");

			return report;
		}

		private static string ExtractValue(string[] lines, string key) {
			foreach (var line in lines)
				if (line.StartsWith(key))
					return line.Substring(key.Length).Trim();
			return "Unknown";
		}

		private static string ExtractValue(string[] lines, int index, string key) {
			return lines[index].Length > key.Length ? lines[index].Substring(key.Length).Trim() : "Unknown";
		}

		private static List<Item> ExtractItems(string[] lines, string section) {
			var items = new List<Item>();
			int index = Array.IndexOf(lines, section);
			if (index != -1) {
				for (int i = index + 1; i < lines.Length; i++) {
					if (String.IsNullOrWhiteSpace(lines[i])) { 
						continue;
					}
					var match = Regex.Match(lines[i], @"(.+), Qty: (\d+)");
					if (match.Success) {
						items.Add(new Item { Name = match.Groups[1].Value.Trim(), Quantity = int.Parse(match.Groups[2].Value) });
					} else {
						items.Add(new Item { Name = lines[i].Trim(), Quantity = 1 });
					}
				}
			}
			return items;
		}
	}

	public class Player {
		public string Name { get; set; }
		public string Corp { get; set; }
		public string Alliance { get; set; }
		public string Faction { get; set; }
		public string Destroyed { get; set; }
	}

	public class InvolvedParty {
		public string Name { get; set; }
		public bool LaidFinalBlow { get; set; } = false;
		public string Security { get; set; } = "Unknown";
		public string Corp { get; set; } = "Unknown";
		public string Alliance { get; set; } = "Unknown";
		public string Faction { get; set; } = "Unknown";
		public string Ship { get; set; } = "Unknown";
		public string Weapon { get; set; } = "Unknown";
		public int DamageDone { get; set; }
	}

	public class Item {
		public string Name { get; set; }
		public int Quantity { get; set; }
	}
}