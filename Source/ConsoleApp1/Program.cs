// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using System.Text.Json.Nodes;

var document = JsonDocument.Parse("[1, 2, 3]");
foreach (var item in document.RootElement.EnumerateArray().Select(x => x.GetString()))
{
    Console.WriteLine(item.GetInt32());
}

Console.WriteLine("Hello, World!");
