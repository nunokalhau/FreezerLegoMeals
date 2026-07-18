using System;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using WebApi.DotNet;
using Repository.DotNet;

namespace WebApi.DotNet.IntegrationTests
{
    [CollectionDefinition("IntegrationTests", DisableParallelization = true)]
    public class IntegrationTestCollection : ICollectionFixture<WebApplicationFactory<WebApi.DotNet.Program>>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the 
        // ICollectionFixture<T> attributes for this collection.
    }

    public abstract class BaseIntegrationTest : IDisposable
    {
        private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
        protected readonly WebApplicationFactory<Program> _factory;
        protected readonly HttpClient _client;

        protected BaseIntegrationTest()
        {
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<FreezerLegoMealsContext>>();
                    services.RemoveAll<FreezerLegoMealsContext>();
                    services.AddDbContext<FreezerLegoMealsContext>(options =>
                        options.UseInMemoryDatabase("TestDatabase", DatabaseRoot));
                });
            });

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FreezerLegoMealsContext>();
                IntegrationTestDbSeeder.SeedTestData(context);
            }

            _client = _factory.CreateClient();
        }

        public void Dispose()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }
    }
}