using MongoDB.Bson;
using MongoDB.Driver;
using System.Net;
using System.Text.Json.Serialization;

namespace Identity.Managers
{
    public class UserManager
    {
        public IMongoCollection<User> _users;

        public UserManager()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("NotesChat");
            _users = database.GetCollection<User>("users");
        }

        public async Task<Either<List<User>, UserError>> GetAll()
        {
            IAsyncCursor<User> result;
            try
            {
                result = await _users.FindAsync(new BsonDocument());
                var users = result.ToList();
                return new Either<List<User>, UserError>(users);
            }
            catch
            {
                return new Either<List<User>, UserError>(UserError.NoDatabaseConnection);
            }
        }

        public async Task<Either<string, UserError>> CreateNew(UserBody input)
        {
            if (await UsernameAlreadyInUse(input.Name))
            {
                return new Either<string, UserError>(UserError.UsernameAlreadyUsed);
            }

            try
            {
                var id = Guid.NewGuid().ToString();
                var user = new User
                {
                    Id = id,
                    Name = input.Name,
                    Password = input.Password,
                };
                await _users.InsertOneAsync(user);

                return new Either<string, UserError>(id);
            }
            catch
            {
                return new Either<string, UserError>(UserError.NoDatabaseConnection);
            }
        }

        public async Task<Either<User, UserError>> FindOne(UserBody userBody)
        {
            try
            {
                var result = (await _users.FindAsync(user => user.Name == userBody.Name)).ToList();
                if (result.Count > 0)
                {
                    return new Either<User, UserError>(result[0]);
                }
                return new Either<User, UserError>(UserError.NotFound);
            }
            catch
            {
                return new Either<User, UserError>(UserError.NoDatabaseConnection);
            }
        }

        public async Task<Either<List<User>, UserError>> FindAll(SearchBody userBody)
        {
            try
            {
                var result = (await _users.FindAsync(user => user.Name.Contains(userBody.Name))).ToList();
                return new Either<List<User>, UserError>(result);
            }
            catch
            {
                return new Either<List<User>, UserError>(UserError.NoDatabaseConnection);
            }
        }

        public async Task<Either<User, UserError>> FindOne(string userId)
        {
            try
            {
                var result = (await _users.FindAsync(user => user.Id == userId)).ToList();
                if (result.Count > 0)
                {
                    return new Either<User, UserError>(result[0]);
                }
                return new Either<User, UserError>(UserError.NotFound);
            }
            catch
            {
                return new Either<User, UserError>(UserError.NoDatabaseConnection);
            }
        }

        public async Task<UserError> DeleteOne(string userId, HttpRequest request)
        {
            try
            {
                await _users.DeleteOneAsync(user => user.Id == userId);

                var cookies = new CookieContainer();
                cookies.Add(new Uri("http://localhost/"), new Cookie("sessionId", request.Cookies["sessionId"]));
                var handler = new HttpClientHandler
                {
                    CookieContainer = cookies
                };

                var _httpClient = new HttpClient(handler);
                _httpClient.DeleteAsync($"http://localhost/api/contacts/list");

                return UserError.None;
            }
            catch
            {
                return UserError.NoDatabaseConnection;
            }
        }

        async Task<bool> UsernameAlreadyInUse(string username)
        {
            IAsyncCursor<User> result;
            try
            {
                result = await _users.FindAsync(new BsonDocument {
                    { "Name" , username }
                });
            }
            catch
            {
                return true;
            }
            return result.Any();
        }
    }

    public struct User
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }

    public struct UserBody
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }

    public struct UserResponseBody
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public struct UserDeleteBody
    {
        [JsonPropertyName("id")]
        public string Id;
    }

    public struct SearchBody
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public enum UserError
    {
        None,
        NoDatabaseConnection,
        NotFound,
        WrongFormatInDatabase,
        UsernameAlreadyUsed
    }
}
