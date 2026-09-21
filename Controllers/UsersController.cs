using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Models;
using UserManagementAPI.Services;
namespace UserManagementAPI.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(UserStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<User>> GetAll() => Ok(store.GetAll());
    [HttpGet("{id:int}")]
    public ActionResult<User> GetById(int id) =>
        store.Get(id) is { } user ? Ok(user) : NotFound();
    [HttpPost]
    public ActionResult<User> Create(UserRequest request)
    {
        var user = store.Create(request);
        return user is null
            ? Problem(statusCode: 409, title: "A user with this email already exists.")
            : CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }
    [HttpPut("{id:int}")]
    public IActionResult Update(int id, UserRequest request) => store.Update(id, request) switch
    {
        UpdateResult.NotFound => NotFound(),
        UpdateResult.DuplicateEmail => Problem(statusCode: 409, title: "A user with this email already exists."),
        _ => NoContent()
    };
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) => store.Delete(id) ? NoContent() : NotFound();
}
