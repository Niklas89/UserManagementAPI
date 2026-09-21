using UserManagementAPI.Models;
namespace UserManagementAPI.Services;

// Singleton exercise store. Locking makes writes and uniqueness checks atomic.
public sealed class UserStore
{
    private readonly object gate = new();
    private readonly Dictionary<int, User> users = new();
    private readonly Dictionary<string, int> emailOwners = new(StringComparer.OrdinalIgnoreCase);
    private int nextId = 1;
    public User[] GetAll()
    {
        User[] snapshot;
        lock (gate) snapshot = users.Values.ToArray();
        // Records are immutable, so sorting the snapshot does not need the store lock.
        Array.Sort(snapshot, (left, right) => left.Id.CompareTo(right.Id));
        return snapshot;
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
            emailOwners.Add(user.Email, user.Id);
            return user;
        }
    }
    public UpdateResult Update(int id, UserRequest request)
    {
        lock (gate)
        {
            if (!users.TryGetValue(id, out var previous)) return UpdateResult.NotFound;
            if (EmailExists(request.Email, id)) return UpdateResult.DuplicateEmail;
            var updated = ToUser(id, request);
            emailOwners.Remove(previous.Email);
            emailOwners.Add(updated.Email, id);
            users[id] = updated;
            return UpdateResult.Updated;
        }
    }
    public bool Delete(int id)
    {
        lock (gate)
        {
            if (!users.Remove(id, out var removed)) return false;
            emailOwners.Remove(removed.Email);
            return true;
        }
    }
    private bool EmailExists(string email, int? exceptId = null) =>
        emailOwners.TryGetValue(email.Trim(), out var ownerId) && ownerId != exceptId;
    private static User ToUser(int id, UserRequest request) =>
        new(id, request.FirstName.Trim(), request.LastName.Trim(), request.Email.Trim());
}
public enum UpdateResult { Updated, NotFound, DuplicateEmail }
