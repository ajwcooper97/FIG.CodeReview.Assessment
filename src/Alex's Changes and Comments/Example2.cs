using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FIG.Assessment;

public class Example2 : Controller
{
    public IConfiguration _config;

    public Example2(IConfiguration config)
        => this._config = config;

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromForm] LoginPostModel model)
    {
        // Alex: All of the SQL/DB functionality should be abstracted into a service or repository class
        using var conn = new SqlConnection(this._config.GetConnectionString("SQL"));

        await conn.OpenAsync();

        // Try/catch for reasons identical to those provided in Example1.cs
        try
        {
            // Alex: Use parameterized queries to avoid SQL injection attacks, stored procedure or ORM would be ideal
            var sql = $"SELECT u.UserID, u.PasswordHash FROM User u WHERE u.UserName='{model.UserName}';";
            using var cmd = new SqlCommand(sql, conn);

            // Alex: I think ideally, there would be some sort of model object that these values are being mapped to
            // instead of using this reader object directly, but since I don't have the rest of the project's context, I am
            // leaving this as it is for now
            using var reader = await cmd.ExecuteReaderAsync();

            // first check user exists by the given username
            if (!reader.Read())
            {
                return this.Redirect("/Error?msg=invalid_username");
            }

            // Alex: Move password validation to separate method. This is a good practice for reusability and readability.
            // Also, I just prefer explicit comparison (== false) over negation (!) for readability, but that's just me.
            // then check password is correct
            if (ValidatePassword(model.Password, (byte[])reader["PasswordHash"]) == false)
            {
                return this.Redirect("/Error?msg=invalid_password");
            }

            // Alex:
            // Unfortunately, it's been a while since I've worked with user claims and authentication, so 
            // I do not have any particular input to provide on this block of code.

            // if we get this far, we have a real user. sign them in
            var userId = (int)reader["UserID"];
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);
            this.HttpContext.SignInAsync(principal);

            return this.Redirect(model.ReturnUrl);
        }
        catch (Exception exception)
        {
            // Alex: add logging here

            // Alex: Do we want to throw an error, or simply redirect to an error page? Requires application's context to know for sure
        }
        finally
        {
            // Alex: Close SQL connection when you're done!
            await conn.CloseAsync();
        }
    }

    /// <summary>
    /// Validates that the provided password matches the password hash stored in the database
    /// </summary>
    /// <param name="password"></param>
    /// <param name="databasePasswordHash"></param>
    /// <returns></returns>
    private async Task<bool> ValidatePassword(string password, byte[] databasePasswordHash)
    {
        var inputPasswordHash = MD5.HashData(Encoding.UTF8.GetBytes(password));

        return databasePasswordHash.SequenceEqual(inputPasswordHash);
    }
}

public class LoginPostModel
{
    public string UserName { get; set; }

    public string Password { get; set; }

    public string ReturnUrl { get; set; }
}
