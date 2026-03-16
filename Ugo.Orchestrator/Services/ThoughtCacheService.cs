using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Ugo.Orchestrator.Services;

public sealed record ThoughtCacheHit(string Prompt, string Response, double Similarity, DateTimeOffset CreatedAt);

public sealed class ThoughtCacheService
{
    private const int VectorSize = 128;

    private readonly string _connectionString;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly ILogger<ThoughtCacheService> _logger;

    public ThoughtCacheService(ILogger<ThoughtCacheService> logger)
    {
        _logger = logger;

        var cacheDirectory = Path.Combine(AppContext.BaseDirectory, "cache");
        Directory.CreateDirectory(cacheDirectory);
        var dbPath = Path.Combine(cacheDirectory, "thought-cache.db");

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        EnsureSchema();
    }

    public async Task<ThoughtCacheHit?> FindNearestAsync(
        string agentName,
        string prompt,
        double threshold = 0.95,
        CancellationToken cancellationToken = default)
    {
        var queryVector = CreateVector(prompt);
        ThoughtCacheHit? bestHit = null;
        var bestSimilarity = threshold;

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Prompt, Response, VectorJson, CreatedAt
                FROM ThoughtCache
                WHERE AgentName = $agentName
                ORDER BY Id DESC
                LIMIT 400;
                """;
            command.Parameters.AddWithValue("$agentName", agentName);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var cachedPrompt = reader.GetString(0);
                var cachedResponse = reader.GetString(1);
                var vectorJson = reader.GetString(2);
                var createdAt = DateTimeOffset.Parse(reader.GetString(3));

                var cachedVector = JsonSerializer.Deserialize<float[]>(vectorJson);
                if (cachedVector is null || cachedVector.Length != VectorSize)
                {
                    continue;
                }

                var similarity = CosineSimilarity(queryVector, cachedVector);
                if (similarity >= bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestHit = new ThoughtCacheHit(cachedPrompt, cachedResponse, similarity, createdAt);
                }
            }
        }
        finally
        {
            _mutex.Release();
        }

        return bestHit;
    }

    public async Task UpsertAsync(
        string agentName,
        string prompt,
        string response,
        CancellationToken cancellationToken = default)
    {
        var vector = CreateVector(prompt);
        var vectorJson = JsonSerializer.Serialize(vector);

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ThoughtCache (AgentName, Prompt, Response, VectorJson, CreatedAt)
                VALUES ($agentName, $prompt, $response, $vectorJson, $createdAt)
                ON CONFLICT(AgentName, Prompt)
                DO UPDATE SET
                    Response = excluded.Response,
                    VectorJson = excluded.VectorJson,
                    CreatedAt = excluded.CreatedAt;
                """;

            command.Parameters.AddWithValue("$agentName", agentName);
            command.Parameters.AddWithValue("$prompt", prompt);
            command.Parameters.AddWithValue("$response", response);
            command.Parameters.AddWithValue("$vectorJson", vectorJson);
            command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ThoughtCache (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AgentName TEXT NOT NULL,
                Prompt TEXT NOT NULL,
                Response TEXT NOT NULL,
                VectorJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UNIQUE(AgentName, Prompt)
            );

            CREATE INDEX IF NOT EXISTS IX_ThoughtCache_AgentName
            ON ThoughtCache(AgentName);
            """;
        command.ExecuteNonQuery();
        _logger.LogInformation("Thought cache initialized.");
    }

    private static float[] CreateVector(string text)
    {
        var vector = new float[VectorSize];
        var tokens = text.ToLowerInvariant().Split([' ', '\t', '\r', '\n', ',', '.', ':', ';', '-', '_', '/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var bucket = Math.Abs(token.GetHashCode()) % VectorSize;
            vector[bucket] += 1f;
        }

        var norm = Math.Sqrt(vector.Sum(value => value * value));
        if (norm <= 0d)
        {
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        if (leftMagnitude <= 0d || rightMagnitude <= 0d)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}