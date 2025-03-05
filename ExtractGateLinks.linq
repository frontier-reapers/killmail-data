<Query Kind="Statements">
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Text.Json.Nodes</Namespace>
  <Namespace>System.Text.Json</Namespace>
</Query>

var path = @"C:\source\FrontierSharp\src\tests\FrontierSharp.UnitTests\TestData\SmartAssembly";
var files = Directory.GetFiles(path, "*.json");
var keys = new List<string>();
foreach (var file in files) {
	var node = JsonNode.Parse(File.ReadAllText(file));
	
	if (!node["assemblyType"].GetValue<string>().Equals("SmartGate")) {
		continue;
	}
	
	if (node["gateLink"] == null) {
		continue;
	}
	
	//node.Dump();

	List<string> inlineData = [
		$"\"{node["id"].GetValue<string>()}\"",
		$"{node["gateLink"]["gatesInRange"].AsArray().Count()}",
		$"{node["gateLink"]["isLinked"].GetValue<bool>().ToString().ToLower()}",
		$"\"{node["gateLink"]["destinationGate"]?.GetValue<string>()}\"",
	];

	var key = $"{node["gateLink"]["isLinked"].GetValue<bool>().ToString().ToLower()}-{node["gateLink"]["gatesInRange"].AsArray().Count()}";
	if (keys.Contains(key)) {
		continue;
	}
	
	Console.WriteLine($"[InlineData({String.Join(", ", inlineData)})]");	
	keys.Add(key);
}

