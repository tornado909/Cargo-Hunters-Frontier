using Frontier.Engine;
using System.Text.Json.Nodes;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void MustThrow(Action action, string message)
{
    bool thrown = false;
    try { action(); } catch { thrown = true; }
    Check(thrown, message);
}

var input = JsonNode.Parse("{\"a/b\":{\"~x\":[1,2]},\"drop\":3}")!;
var changes = JsonNode.Parse("[{\"op\":\"set\",\"path\":\"/a~1b/~0x/0\",\"value\":7},{\"op\":\"append\",\"path\":\"/a~1b/~0x\",\"value\":9},{\"op\":\"remove\",\"path\":\"/drop\"}]")!.AsArray();
var changed = PatchEngine.ApplyEdits(input, changes);
Check(changed["a/b"]!["~x"]![0]!.GetValue<int>() == 7, "JSON pointer escaping");
Check(changed["a/b"]!["~x"]!.AsArray().Count == 3 && changed["drop"] is null, "Append/remove");
var root = JsonNode.Parse("[{\"Id\":1,\"v\":[1]}]")!;
var cloneOp = JsonNode.Parse("{\"op\":\"clone\",\"key\":\"Id\",\"source\":1,\"id\":2,\"edits\":[{\"op\":\"set\",\"path\":\"/Id\",\"value\":2},{\"op\":\"append\",\"path\":\"/v\",\"value\":3}]}")!.AsObject();
PatchEngine.ApplyOperation(root, cloneOp);
Check(root[0]!["v"]!.AsArray().Count == 1 && root[1]!["v"]!.AsArray().Count == 2, "Clone must not mutate donor");
MustThrow(() => PatchEngine.ApplyOperation(root, cloneOp), "Duplicate clone must throw");
MustThrow(() => PatchEngine.ApplyEdits(root, JsonNode.Parse("[{\"op\":\"set\",\"path\":\"/9\",\"value\":0}]")!.AsArray()), "Invalid path must throw");
var filters = JsonNode.Parse("{\"filter\":{\"$t\":23661,\"_filterPolicy\":0,\"_templateIds\":[\"a\"]},\"untouched\":{\"_templateIds\":[\"a\"]}}")!;
var aliases = JsonNode.Parse("{\"a\":[\"b\"]}")!.AsObject();
PatchEngine.ExpandAliases(filters, aliases); PatchEngine.ExpandAliases(filters, aliases);
Check(filters["filter"]!["_templateIds"]!.AsArray().Count == 2 && filters["untouched"]!["_templateIds"]!.AsArray().Count == 1, "Scoped idempotent alias expansion");
int[] weights = {70,18,8,3,1}; int[] expected = {0,0,1,1,2,2,3,3,4}; int[] samples = {0,69,70,87,88,95,96,98,99};
for (int i = 0; i < samples.Length; i++) Check(WeightedRoll.Pick(samples[i], weights) == expected[i], "Rarity boundary");
MustThrow(() => WeightedRoll.Pick(100, weights), "Invalid RNG input must throw");
Console.WriteLine("PASS: 16 managed-engine assertions (not native hooks)");

CoopTests.Run();

if (args.Length >= 2)
{
    string packs = Path.GetFullPath(args[0]), repositories = Path.GetFullPath(args[1]);
    var lockFile = PatchEngine.ReadObject(Path.Combine(packs, "compatibility.json"));
    var ids = ((JsonArray)lockFile["moduleIds"]!).Select(n => n!.GetValue<string>()).ToArray();
    int cases = args.Contains("--all-combinations") ? (1 << ids.Length) : 2;
    for (int iteration = 0; iteration < cases; iteration++)
    {
        int mask = cases == 2 ? (iteration == 0 ? 0 : (1 << ids.Length) - 1) : iteration;
        var enabled = ids.Where((id, bit) => (mask & (1 << bit)) != 0).ToArray();
        var engine = new PatchEngine(packs, enabled);
        foreach (string name in engine.RepositoryNames)
        {
            byte[] original = File.ReadAllBytes(Path.Combine(repositories, name));
            byte[] patched = engine.Transform(name, original);
            Check(patched.SequenceEqual(engine.Transform(name, original)), "Repeat-call output must be stable");
            MustThrow(() => engine.Transform(name, patched), "Double patch/hash mismatch must throw");
            var json = JsonNode.Parse(patched)!;
            PatchEngine.EnsureUniqueIds(json);
            if (name == "item_templates") Check(json.AsArray().Count == 1636, "Item catalog size");
            if (name == "bot_presets") Check(json.AsArray().Count == 83, "Bot catalog size");
            if (name == "skills") Check(json.AsArray().Count == 29, "Skill catalog size");
            if (name == "level_progress_settings") Check((int)json["MaxLevel"]! == (enabled.Contains("progression") ? 50 : 25), "Progression toggle");
        }
    }
    Console.WriteLine($"PASS: {cases} actual-repository module combinations in C#; native game execution remains untested");
}

// 0.3 regression tests: test offers are gated, deterministic, distinct and preserve existing entries.
var testManifest = JsonNode.Parse(@"
{""testShopId"":""shop"",""testOffers"":[
 {""module"":""armory"",""id"":""Frontier_TEST_weapon"",""templateId"":""weapon"",""count"":1,""item"":{""$t"":1055,""ItemsPresetId"":""assembled""}},
 {""module"":""salvage"",""id"":""Frontier_TEST_servo"",""templateId"":""servo"",""count"":5,""item"":{""$t"":23772,""ItemItemplateId"":""servo""}}
]}
")!.AsObject();
JsonNode TestShop() => JsonNode.Parse("[{\"Id\":\"shop\",\"PricelistTemplate\":{\"Commodities\":[{\"Id\":\"vanilla\",\"Count\":9}]}}]")!;
var disabledTest = TestShop(); RuntimeTestContent.Apply("shop_templates", disabledTest, testManifest, _ => false);
Check(disabledTest[0]!["PricelistTemplate"]!["Commodities"]!.AsArray().Count == 1, "Disabled modules receive no test offers");
var activeTest = TestShop(); RuntimeTestContent.Apply("shop_templates", activeTest, testManifest, _ => true);
var offers = activeTest[0]!["PricelistTemplate"]!["Commodities"]!.AsArray();
Check(offers.Count == 3 && offers[0]!["Count"]!.GetValue<int>() == 9, "Existing trader stock preserved");
Check(offers[1]!["Item"]!["ItemsPresetId"]!.GetValue<string>() == "assembled", "Test firearm uses assembled preset");
Check(offers[1]!["UnlockRequirement"]!["_minAccountLevel"]!.GetValue<int>() == 1, "Test offer available at level one");
Check(offers[1]!["Chance"]!.GetValue<double>() == 1.0, "Test offer is not random");
Check(offers[2]!["Count"]!.GetValue<int>() == 5, "Salvage test quantity");
MustThrow(() => RuntimeTestContent.Apply("shop_templates", activeTest, testManifest, _ => true), "Repeated test append must fail");
var sigBase = new PackSettings(); var sigTest = new PackSettings { RuntimeTestMode = true };
Check(CoopProtocol.Fingerprint("test", sigBase, new Dictionary<string,string>()) != CoopProtocol.Fingerprint("test", sigTest, new Dictionary<string,string>()), "Test mode changes peer signature");
Console.WriteLine("PASS: 8 runtime-fix managed regression assertions. Native game behavior NOT exercised.");

// Regression for connection allocation after the ConnectToShelter prefix.
ShelterConnectionTests.Run();
