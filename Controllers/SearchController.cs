using Identity.Managers;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text.Json;

namespace Identity.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class SearchController : Controller
    {
        private readonly UserManager _users;

        public SearchController(UserManager userManager, SessionManager sessions)
        {
            _users = userManager;
        }

        [HttpPost]
        public async Task<dynamic> SearchAll()
        {
            string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
            SearchBody data;

            try
            {
                data = JsonSerializer.Deserialize<SearchBody>(requestBody);
            }
            catch
            {
                return StatusCode(400, new { cause = "missing body" });
            }
            return (await _users.FindAll(data)).Match<ActionResult>(
                users =>
                {
                    var userRes = users.Select(user => new
                    {
                        Id = user.Id,
                        Name = user.Name
                    });
                    return StatusCode(200, new { users = userRes });
                },
                err =>
                {
                    return StatusCode(500, new { cause = "search failed" });
                }
            );
        }
    }
}