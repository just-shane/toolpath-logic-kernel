using System;
using System.IO;
using System.Text.Json;

namespace TLK.Core
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("--- TLK Kinematic Engine Initializing ---");

           // 1. Locate the exported JSON from the UI
            // AppContext.BaseDirectory puts us in bin/Debug/net10.0/. We need to go up 5 levels.
            string jsonPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tlk-schemas", "active_program.json"));

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"[ERROR] Could not find active program at: {jsonPath}");
                return;
            }
            // 2. Read and parse the JSON document
            string jsonString = File.ReadAllText(jsonPath);
            using JsonDocument doc = JsonDocument.Parse(jsonString);
            JsonElement root = doc.RootElement;

            Console.WriteLine($"SUCCESS: Loaded {jsonPath}\n");
            Console.WriteLine("--- Digesting Nodes ---");

            // 3. Dig into the "nodes" dictionary and map their IDs
            Dictionary<string, string> nodeDictionary = new Dictionary<string, string>();

            if (root.TryGetProperty("nodes", out JsonElement nodesElement))
            {
                foreach (JsonProperty node in nodesElement.EnumerateObject())
                {
                    string nodeId = node.Name; // Grabs the "0x..." ID
                    JsonElement nodeData = node.Value;
                    
                    if (nodeData.TryGetProperty("type_", out JsonElement typeElement))
                    {
                        string nodeType = typeElement.GetString() ?? "";
                        string cleanType = nodeType.Split('.')[^1];

                        if (nodeData.TryGetProperty("custom", out JsonElement props))
                        {
                            if (cleanType == "MachineNode")
                            {
                                string model = props.GetProperty("machine_model").GetString() ?? "Unknown";
                                Console.WriteLine($"> Initializing Environment: {model}");
                                nodeDictionary[nodeId] = $"Machine ({model})";
                            }
                            else if (cleanType == "TurnCycleNode")
                            {
                                string tool = props.GetProperty("tool_call").GetString() ?? "Unknown";
                                string endZ = props.GetProperty("end_z").GetString() ?? "0.0";
                                Console.WriteLine($"> Found Machining Cycle: Turn OD to Z{endZ} using {tool}");
                                nodeDictionary[nodeId] = $"Turn OD (Z{endZ})";
                            }
                            else if (cleanType == "SyncNode")
                            {
                                string syncNum = props.GetProperty("sync_number").GetString() ?? "0";
                                Console.WriteLine($"> Found Channel Sync: Wait Code {syncNum}");
                                nodeDictionary[nodeId] = $"Wait/Sync (!{syncNum})";
                            }
                        }
                    }
                }
            }

            Console.WriteLine("\n--- Tracing Logic Flow ---");

            // 4. Parse the "connections" array to map the wires
            if (root.TryGetProperty("connections", out JsonElement connectionsElement))
            {
                foreach (JsonElement conn in connectionsElement.EnumerateArray())
                {
                    // "out" is an array: ["NodeID", "PinName"]
                    JsonElement outPin = conn.GetProperty("out");
                    string fromId = outPin[0].GetString() ?? "";
                    string fromPort = outPin[1].GetString() ?? "";

                    // "in" is an array: ["NodeID", "PinName"]
                    JsonElement inPin = conn.GetProperty("in");
                    string toId = inPin[0].GetString() ?? "";
                    string toPort = inPin[1].GetString() ?? "";

                    // Look up our friendly names using the dictionary
                    string fromName = nodeDictionary.ContainsKey(fromId) ? nodeDictionary[fromId] : "Unknown";
                    string toName = nodeDictionary.ContainsKey(toId) ? nodeDictionary[toId] : "Unknown";

                    Console.WriteLine($"[Wire] {fromName} ({fromPort})  --->  {toName} ({toPort})");
                }
            }
            
            Console.WriteLine("\n--- Awaiting F# Kinematic Processing ---");
        }
    }
}