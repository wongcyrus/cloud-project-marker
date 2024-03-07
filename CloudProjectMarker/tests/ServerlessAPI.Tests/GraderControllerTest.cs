using ServerlessAPI.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace ServerlessAPI.Tests;

public class GraderControllerTest
{
    private readonly WebApplicationFactory<Program> webApplication;
    public GraderControllerTest()
    {
        webApplication = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    //Mock the repository implementation
                    //to remove infra dependencies for Test project
                 
                });
            });
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public async Task Call_GetApiBooks_ShouldReturn_LimitedListOfBooks(int limit)
    {
        var client = webApplication.CreateClient();
        var books = await client.GetFromJsonAsync<IList<Book>>($"/api/Books?limit={limit}");

        Assert.NotEmpty(books);
        Assert.Equal(limit, books?.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Call_GetApiBook_ShouldReturn_BadRequest(int limit)
    {
        var client = webApplication.CreateClient();
        var result = await client.GetAsync($"/api/Books?limit={limit}");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result?.StatusCode);

    }
}