using MongoDB.Driver;
using System.Text.Json.Serialization;

namespace Identity.Managers
{
    public class SessionManager
    {
        public IMongoCollection<Session> _sessions;
        public UserManager _users;

        public SessionManager(UserManager users)
        {
            _users = users;

            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("NotesChat");
            _sessions = database.GetCollection<Session>("sessions");
        }

        public async Task<Either<string, SessionError>> GenerateNew(string userId)
        {
            try
            {
                var id = Guid.NewGuid().ToString();
                var user = new Session
                {
                    Id = id,
                    UserId = userId,
                };
                await _sessions.InsertOneAsync(user);

                return new Either<string, SessionError>(id);
            }
            catch
            {
                return new Either<string, SessionError>(SessionError.NoDatabaseConnection);
            }
        }

        public async Task<Either<Either<User, UserError>, SessionError>> FindOne(string sessionId)
        {
            try
            {
                var result = (await _sessions.FindAsync(session => session.Id == sessionId)).ToList();
                if (result.Count > 0)
                {
                    var user = await _users.FindOne(result[0].UserId);
                    return new Either<Either<User, UserError>, SessionError>(user);
                }
                return new Either<Either<User, UserError>, SessionError>(SessionError.NotFound);
            }
            catch
            {
                return new Either<Either<User, UserError>, SessionError>(SessionError.NoDatabaseConnection);
            }
        }

        public async Task<bool> isLoggedIn(HttpRequest request)
        {
            var sessionId = request.Cookies["sessionId"];
            if (sessionId == null)
            {
                return false;
            }
            var res = await FindOne(sessionId);
            return res.Match(
                user =>
                {
                    return user.Match(
                        user =>
                        {
                            return true;
                        },
                        err =>
                        {
                            return false;
                        }
                    );
                },
                err =>
                {
                    return false;
                }
            );
        }

        public async Task<Either<User, SessionError>> getUserFromRequest(HttpRequest request)
        {
            var sessionId = request.Cookies["sessionId"];
            if (sessionId == null)
            {
                return new Either<User, SessionError>(SessionError.NotFound);
            }
            var res = await FindOne(sessionId);
            return res.Match(
                user =>
                {
                    return user.Match(
                        user =>
                        {
                            return new Either<User, SessionError>(user);
                        },
                        err =>
                        {
                            return new Either<User, SessionError>(SessionError.NoDatabaseConnection);
                        }
                    );
                },
                err =>
                {
                    return new Either<User, SessionError>(SessionError.NoDatabaseConnection);
                }
            );
        }
    }

    public struct Session
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("userId")]
        public string UserId { get; set; }
    }

    public enum SessionError
    {
        NoDatabaseConnection,
        NotFound,
    }
}
