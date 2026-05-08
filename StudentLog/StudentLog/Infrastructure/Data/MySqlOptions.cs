namespace StudentLog.Infrastructure.Data;

public class MySqlOptions
{
    public string Server { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 3307;
    public string Database { get; init; } = "student_logDb";
    public string UserId { get; init; } = "root";
    public string Password { get; init; } = "FIL2026";

    public string ConnectionString => $"Server={Server};Port={Port};Database={Database};User ID={UserId};Password={Password};SslMode=Preferred;Allow User Variables=True;";
}
