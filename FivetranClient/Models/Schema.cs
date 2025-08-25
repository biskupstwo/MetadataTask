namespace FivetranClient.Models;

public class Schema
{
    public string NameInDestination { get; set; } = String.Empty;
    public bool Enabled { get; set; }
    public Dictionary<string, Table> Tables { get; set; } = new();
}