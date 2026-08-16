namespace SecurePrReviewer.Core.Users;

public sealed class UserRepository
{
    public string BuildQuery(string name)
    {
        var sql = $"SELECT * FROM Users WHERE Name = '{name}'";
        return sql;
    }
}
