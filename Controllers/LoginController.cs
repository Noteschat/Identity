using Identity.Managers;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Identity.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class LoginController : Controller
    {
        private readonly ILogger<LoginController> _logger;
        private readonly UserManager _users;
        private readonly SessionManager _sessions;

        public LoginController(ILogger<LoginController> logger, UserManager users, SessionManager sessions)
        {
            _logger = logger;
            _users = users;
            _sessions = sessions;
        }

        [HttpPost]
        public async Task<dynamic> Login()
        {
            string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
            UserBody data;

            try
            {
                data = JsonSerializer.Deserialize<UserBody>(requestBody);
            }
            catch
            {
                return StatusCode(400, new { cause = "missing body" });
            }

            var result = await _users.FindOne(data);

            return await result.Match<ActionResult>(
                async user =>
                {
                    if (user.Password != data.Password)
                    {
                        return StatusCode(404, new { cause = "password wrong" });
                    }
                    var res = await _sessions.GenerateNew(user.Id);
                    return res.Match<ActionResult>(
                        id =>
                        {
                            Response.Cookies.Append("sessionId", id);
                            return StatusCode(200);
                        },
                        err =>
                        {
                            return StatusCode(500, new { cause = "session creation failed" });
                        }
                    );
                },
                async error =>
                {
                    switch(error)
                    {
                        case UserError.NotFound:
                            return StatusCode(404, new { cause = "unknown user" });
                        default:
                            return StatusCode(500, new { cause = "login failed" });
                    }
                }
            );
        }

        [HttpGet("valid")]
        public async Task<dynamic> valid()
        {
            var Result = await _sessions.getUserFromRequest(Request);
            return Result.Match<IActionResult>(
                user =>
                {
                    return StatusCode(200, new UserResponseBody
                    {
                        Id = user.Id,
                        Name = user.Name
                    });
                },
                error =>
                {
                    return StatusCode(404);
                }
            );
        }
    }
}
