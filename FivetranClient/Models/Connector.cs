namespace FivetranClient.Models;

public class Connector
{
    public string Id { get; set; } = String.Empty;
    public string Service { get; set; } = String.Empty;
    public string Schema { get; set; } = String.Empty;
    public bool Paused { get; set; }
}