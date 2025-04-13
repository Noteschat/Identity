using Identity.Managers;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text.Json;

namespace Identity.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class UserController : Controller
    {
        private readonly UserManager _users;
        private readonly SessionManager _sessions;

        public UserController(UserManager userManager, SessionManager sessions)
        {
            _users = userManager;
            _sessions = sessions;
        }

        [HttpGet]
        public async Task<dynamic> GetAll()
        {
            var user = await _sessions.getUserFromRequest(Request);
            if(!user.IsSuccess)
            {
                switch (user.Error)
                {
                    case SessionError.NotFound:
                        return StatusCode(401, new { cause  = "not logged in" });
                    default:
                        return StatusCode(500, new { cause = "retrieval failed" });
                }
            }
            var users = await _users.GetAll();
            return users.Match<IActionResult>(
                users => {
                    var response = users.Select(user => new UserResponseBody
                    {
                        Id = user.Id,
                        Name = user.Name,
                    }).ToList();
                    return StatusCode(200, new { users = response });
                },
                error => StatusCode(500, new { cause = "retrieval failed" })
            );
        }

        [HttpPost]
        public async Task<dynamic> CreateNew()
        {
            string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
            UserBody data;

            try
            {
                data = JsonSerializer.Deserialize<UserBody>(requestBody);
            }
            catch
            {
                return StatusCode(400, new { cause = "missing properties" });
            }

            var result = await _users.CreateNew(data);
            return result.Match<IActionResult>(
                id => StatusCode(200, new { id }),
                error =>
                {
                    switch(error)
                    {
                        case UserError.UsernameAlreadyUsed:
                            return StatusCode(400, new { cause = "username already in use" });
                        default:
                            return StatusCode(500, new { cause = "insertion failed" });
                    }
                }
            );
        }

        [HttpGet("{userId}")]
        public async Task<dynamic> GetOne(string userId)
        {
            var user = await _users.FindOne(userId);
            return user.Match<IActionResult>(
                user => {
                    var response = new UserResponseBody
                    {
                        Id = user.Id,
                        Name = user.Name,
                    };
                    return StatusCode(200, response);
                },
                error => {
                    switch(error)
                    {
                        case UserError.NotFound:
                            return StatusCode(404, new { cause = "user not found" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpGet("current")]
        public async Task<dynamic> GetCurrent()
        {
            var res = await _sessions.getUserFromRequest(Request);
            return res.Match<IActionResult>(
                user => {
                    var response = new UserResponseBody
                    {
                        Id = user.Id,
                        Name = user.Name,
                    };
                    return StatusCode(200, response);
                },
                error => {
                    switch (error)
                    {
                        case SessionError.NotFound:
                            return StatusCode(401, new { cause = "not logged in" });
                        default:
                            return StatusCode(500, new { cause = "retrieval failed" });
                    }
                }
            );
        }

        [HttpDelete("current")]
        public async Task<dynamic> DeleteCurrent()
        {
            var res = await _sessions.getUserFromRequest(Request);
            return await res.Match<ActionResult>(
                async (user) =>
                {
                    var result = await _users.DeleteOne(user.Id);
                    switch (result)
                    {
                        case UserError.None:
                            return StatusCode(200);
                        default:
                            return StatusCode(500, new { cause = "deletion failed" });
                    }
                },
                async (error) =>
                {
                    switch(error)
                    {
                        case SessionError.NotFound:
                            return StatusCode(401, new { cause = "not logged in" });
                        default:
                            return StatusCode(500, new { cause = "deletion failed" });
                    }
                }
            );
        }
    }
}
