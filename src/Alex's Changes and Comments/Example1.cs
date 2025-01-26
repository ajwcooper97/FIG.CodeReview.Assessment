using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FIG.Assessment;

/// <summary>
/// In this example, the goal of this GetPeopleInfo method is to fetch a list of Person IDs from our database (not implemented / not part of this example),
/// and for each Person ID we need to hit some external API for information about the person. Then we would like to return a dictionary of the results to the caller.
/// We want to perform this work in parallel to speed it up, but we don't want to hit the API too frequently, so we are trying to limit to 5 requests at a time at most
/// by using 5 background worker threads.
/// Feel free to suggest changes to any part of this example.
/// In addition to finding issues and/or ways of improving this method, what is a name for this sort of queueing pattern?
/// </summary>
public class Example1
{
    // ********************************************************************************************************************
    // Alex: NOTE - I don't usually write THIS many comments or put my name on them, this is just a combination of explanations for
    // the assesment, and to simulate a code review. I would normally just write comments as needed
    // ********************************************************************************************************************

    // Alex: Move http client to instance variable to avoid creating multiple instances for each function call of GatherInfo
    // Ideally, there would be some sort of scoped or singleton service in the project that handles this functionality
    private readonly HttpClient _client;

    // Alex:
    // Convert existing methods to an async context. Generally speaking, I believe it is better to use 
    // async over synchronous methods to avoid blocking the main thread, especially if this is running in a front end application
    public Example1()
    {
        _client = new HttpClient();
    }

    public async Task<Dictionary<int, int>> GetPeopleInfo()
    {
        // Alex: Add logging to method call - could add line for DB logging here too. I would use an external class dedicated to logging
        Console.WriteLine("INFO: Executing 'GetPeopleInfo'... (or something standardized here)");

        // Alex: Convert to concurrent objects for thread-safe parallel access
        // initialize empty queue, and empty result set
        var personIdQueue = new ConcurrentQueue<int>();
        var results = new ConcurrentDictionary<int, int>();

        // Alex:
        // Wrap in a try-catch block for error handling
        // Log exception to both console and a hypothetical db log or service, so that the error can be tracked even in higher lanes where
        // debugging may be more difficult.
        try
        {
            // Alex:
            // Retrieve person IDs from the database before proceeding - if there is an error during retrieval,
            // then there is no reason to continue onto the rest of the code
            await CollectPersonIds(personIdQueue);

            // Alex:
            // Could use Parallel.ForEach here - or ForEachAsync depending on .NET version. For this purpose, I think it may be overkill
            // and could negatively impact performance, though I am unsure. Since we are using threads/tasks, it is generally better to
            // use the Task class instead of threads directly. It tends to be simpler, and easier to manage. Let C# do the work for you. 
            // Renamed gatherThreads to gatherTasks to represent the change to Task objects

            // start 5 worker threads to read through the queue and fetch info on each item, adding to the result set
            var gatherTasks = new List<Task>(5);

            for (var i = 0; i < 5; i++)
            {
                gatherTasks.Add(GatherInfo(personIdQueue, results));
            }

            // wait for all threads to finish
            await Task.WhenAll(gatherTasks);
        }
        catch (Exception exception)
        {
            // Could include other information such as the personIdQueue data, etc for debugging purposes
            Console.WriteLine(exception);

            // db/service logging here

            // could 'throw;' here to bubble the error up, or not, really depends on the context of the application
        }

        return results;
    }

    private async Task CollectPersonIds(Queue<int> personIdQueue)
    {
        // Alex: add logging to method call
        Console.WriteLine("INFO: Executing 'CollectPersonIds'");

        // dummy implementation, would be pulling from a database
        for (var i = 1; i < 100; i++)
        {
            if (i % 10 == 0) Thread.Sleep(TimeSpan.FromMilliseconds(50)); // artificial delay every now and then
            personIdQueue.Enqueue(i);
        }
    }

    // Alex:
    // VERY IMPORTANT - I did not decide to add it here, but it would be wise to have some sort of lock for any errors that occur during
    // API calls. For example, if we receive an error that we are sending too many requests in too short of a time,
    // and the API provides us with a wait time, we can use a lock to prevent the threads from continuing until the wait time is over.
    // This would prevent us from being rate limited by the API. i.e. SemaphoreSlim
    // If this were a code review, I may say "we can do it in the next sprint" if there is not time to implement it now, but I would suggest doing so ASAP
    private async Task GatherInfo(ConcurrentQueue<int> personIdQueue, ConcurrentDictionary<int, int> results)
    {
        // Alex: did not add logging here, since we would just see 5 of these in a row. Could add logging
        // elsewhere in the method if needed, but fine without it for now

        // pull IDs off the queue until it is empty
        while (personIdQueue.TryDequeue(out var id))
        {
            try
            {
                // Alex: Move these into using statements to both show the scope of the objects, and to ensure they are disposed of properly
                using (var request = new HttpRequestMessage(HttpMethod.Get, $"https://some.example.api/people/{id}/age"))
                {
                    using (var response = await _client.SendAsync(request))
                    {
                        var age = int.Parse(await response.Content.ReadAsStringAsync());

                        // Alex: Use the dictionary's methods for manipulating data, and take advantage of the thread-safe nature of ConcurrentDictionary
                        results.Add(id, age);
                    }
                }
            }
            catch (Exception exception)
            {
                // Alex: Add logging for exceptions
                Console.WriteLine(exception);

                // db/service logging here

                // could 'throw;' here to bubble the error up, or not, or just proceed to next item in queue
                // This is where locks could be implemented or applied as well, depending on the error
            }
        }
    }
}
