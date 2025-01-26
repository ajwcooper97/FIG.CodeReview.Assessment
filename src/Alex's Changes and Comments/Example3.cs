using System.Threading.Tasks;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FIG.Assessment;

// Alex:
// My first question regarding this service is, if it is only run once every 24 hours, is it necessary to run it as a background service?
// This could be a task that is triggered once a day via something like Autosys or an Azure Webjob
// Did not include any changes or anything to actually call DailyReportService.ExecuteAsync(), not sure if that was expected or not

/// <summary>
/// In this example, we are writing a service that will run (potentially as a windows service or elsewhere) and once a day will run a report on all new
/// users who were created in our system within the last 24 hours, as well as all users who deactivated their account in the last 24 hours. We will then
/// email this report to the executives so they can monitor how our user base is growing.
/// </summary>
public class Example3
{
    public static void Main(string[] args)
    {
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddDbContext<MyContext>(options =>
                {
                    options.UseSqlServer("dummy-connection-string");
                });
                services.AddSingleton<ReportEngine>();
                services.AddHostedService<DailyReportService>();
            })
            .Build()
            .Run();
    }
}

public class DailyReportService : BackgroundService
{
    private readonly ReportEngine _reportEngine;

    public DailyReportService(ReportEngine reportEngine) => _reportEngine = reportEngine;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Alex: Added try/catch to main method, though could have more granular catch blocks deeper in the code, such as in 'SendUserReportAsync'
        try
        {
            // Alex: May be best to use UTC time instead of local time here.
            // when the service starts up, start by looking back at the last 24 hours
            var startingFrom = DateTime.Now.AddDays(-1);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Alex: Removed 'this.' - it is redundant. Not an important change.
                var newUsersTask = _reportEngine.GetNewUsersAsync(startingFrom);
                var deactivatedUsersTask = _reportEngine.GetDeactivatedUsersAsync(startingFrom);
                await Task.WhenAll(newUsersTask, deactivatedUsersTask); // run both queries in parallel to save time

                // send report to execs
                await SendUserReportAsync(newUsersTask.Result, deactivatedUsersTask.Result);

                // Alex:
                // This information should ABSOLUTELY be stored outside of the program - either in a database, or a local config file, if the
                // program is not converted to a triggered webjob or something similar instead of a background service. 
                // I would reject a PR using this code with this in mind, and provide the above as the reason.
                // More politely, though, than how I am writing it here.

                // save the current time, wait 24hr, and run the report again - using the new cutoff date
                startingFrom = DateTime.Now;
                await Task.Delay(TimeSpan.FromHours(24));
            }
        }
        catch (Exception exception)
        {
            // Alex: error handling and logging here
        }
    }

    private Task SendUserReportAsync(IEnumerable<User> newUsers, IEnumerable<User> deactivatedUsers)
    {
        // not part of this example
        return Task.CompletedTask;
    }
}

/// <summary>
/// A dummy report engine that runs queries and returns results.
/// The queries here a simple but imagine they might be complex SQL queries that could take a long time to complete.
/// </summary>
public class ReportEngine
{
    private readonly MyContext _db;

    public ReportEngine(MyContext db) => _db = db;

    public async Task<IEnumerable<User>> GetNewUsersAsync(DateTime startingFrom)
    {
        var newUsers = await _db.Users
            .Where(u => u.CreatedAt >= startingFrom)
            .ToListAsync();

        // Alex: the code below is pulling the entire list into memory, then filtering it. This is inefficient, and needed to be rewritten
        // to the code used above
        //var newUsers = (await this._db.Users.ToListAsync())
        //    .Where(u => u.CreatedAt > startingFrom);

        return newUsers;
    }

    public async Task<IEnumerable<User>> GetDeactivatedUsersAsync(DateTime startingFrom)
    {
        var deactivatedUsers = await _db.Users
            .Where(u => u.DeactivatedAt >= startingFrom)
            .ToListAsync();

        // Alex: the code below is pulling the entire list into memory, then filtering it. This is inefficient, and needed to be rewritten
        // to the code used above
        //var deactivatedUsers = (await this._db.Users.ToListAsync())
        //    .Where(u => u.DeactivatedAt > startingFrom);

        return deactivatedUsers;
    }
}

#region Database Entities
// a dummy EFCore dbcontext - not concerned with actually setting up connection strings or configuring the context in this example
public class MyContext : DbContext
{
    public DbSet<User> Users { get; set; }
}

public class User
{
    public int UserId { get; set; }

    public string UserName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeactivatedAt { get; set; }
}
#endregion