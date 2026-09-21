using UserManagementAPI.Models;
namespace UserManagementAPI.Services;

// Singleton exercise store. Locking makes writes and uniqueness checks atomic.
public sealed class UserStore
{
    private readonly object gate = new();
    private readonly Dictionary<int, User> users = new();
    private int nextId = 1;
    public User[] GetAll()
    {
        lock (gate) return users.Values.OrderBy(user => user.Id).ToArray();
    }
    public User? Get(int id)
    {
        lock (gate) return users.GetValueOrDefault(id);
    }
    public User? Create(UserRequest request)
    {
        lock (gate)
        {
            if (EmailExists(request.Email)) return null;
            var user = ToUser(nextId++, request);
            users.Add(user.Id, user);
            return user;
        }
    }
    public UpdateResult Update(int id, UserRequest request)
    {
        lock (gate)
        {
            if (!users.ContainsKey(id)) return UpdateResult.NotFound;
            if (EmailExists(request.Email, id)) return UpdateResult.DuplicateEmail;
            users[id] = ToUser(id, request);
            return UpdateResult.Updated;
        }
    }
    public bool Delete(int id)
    {
        lock (gate) return users.Remove(id);
    }
    private bool EmailExists(string email, int? exceptId = null) =>
        users.Values.Any(user => user.Id != exceptId &&
            string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
    private static User ToUser(int id, UserRequest request) =>
        new(id, request.FirstName.Trim(), request.LastName.Trim(), request.Email.Trim());
}
public enum UpdateResult { Updated, NotFound, DuplicateEmail }
