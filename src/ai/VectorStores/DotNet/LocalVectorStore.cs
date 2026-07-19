using System.Text.Json;

namespace VectorStores.DotNet;

public sealed class LocalVectorStore : IVectorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _embeddingsDirectory;
    private IReadOnlyList<StoredEmbedding>? _cache;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public LocalVectorStore(string embeddingsDirectory)
    {
        _embeddingsDirectory = embeddingsDirectory;
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        if (topK <= 0)
            return [];

        var embeddings = await LoadEmbeddingsOnceAsync(cancellationToken);
        return embeddings
            .Select(embedding => new VectorMatch(embedding.RecipeId, CosineSimilarity.Calculate(queryEmbedding, embedding.Embedding)))
            .OrderByDescending(match => match.Score)
            .Take(topK)
            .ToList();
    }

    private async Task<IReadOnlyList<StoredEmbedding>> LoadEmbeddingsOnceAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
            return _cache;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache is not null)
                return _cache;

            if (!Directory.Exists(_embeddingsDirectory))
            {
                _cache = [];
                return _cache;
            }

            var loaded = new List<StoredEmbedding>();
            foreach (var path in Directory.EnumerateFiles(_embeddingsDirectory, "*.json").OrderBy(path => path))
            {
                await using var stream = File.OpenRead(path);
                var payload = await JsonSerializer.DeserializeAsync<EmbeddingFile>(
                    stream,
                    JsonOptions,
                    cancellationToken: cancellationToken);
                if (payload?.Embedding is { Count: > 0 })
                {
                    loaded.Add(new StoredEmbedding(
                        string.IsNullOrWhiteSpace(payload.RecipeId) ? Path.GetFileNameWithoutExtension(path) : payload.RecipeId,
                        payload.Embedding));
                }
            }

            // TODO: Move this cache to Redis if local process memory becomes insufficient.
            _cache = loaded;
            return _cache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private sealed record EmbeddingFile(string RecipeId, IReadOnlyList<float> Embedding);

    private sealed record StoredEmbedding(string RecipeId, IReadOnlyList<float> Embedding);
}