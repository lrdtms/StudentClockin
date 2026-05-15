namespace StudentLog.Infrastructure.Data;

public class MySqlOptions
{
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Database { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string ConnectionString =>
        $"Server={Server};Port={Port};Database={Database};User ID={UserId};Password={Password};SslMode=Preferred;Allow User Variables=True;";
}
