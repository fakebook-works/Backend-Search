using BackEndSearchFakebook.Models;
using Microsoft.EntityFrameworkCore;

namespace BackEndSearchFakebook.Infrastructure;

public sealed class SearchFeedbackSchemaHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SearchFeedbackSchemaHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FakebookMinhContext>();
        await database.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS search.search_object_views (
                user_id BIGINT NOT NULL,
                object_id BIGINT NOT NULL REFERENCES search.objects(id) ON DELETE CASCADE,
                viewed_on DATE NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (user_id, object_id, viewed_on),
                CONSTRAINT ck_search_object_views_user_positive CHECK (user_id > 0),
                CONSTRAINT ck_search_object_views_object_positive CHECK (object_id > 0)
            );

            CREATE INDEX IF NOT EXISTS ix_search_object_views_object_date
                ON search.search_object_views (object_id, viewed_on DESC);
            """,
            cancellationToken);
        logger.LogInformation("Search feedback schema is ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
