namespace Services.DotNet.Contracts
{
    public sealed class RecipeInfoResponse
    {
        public int Id { get; init; }

        public string Name { get; init; }

        public int Servings { get; init; }

        public int TimeToPrepare { get; init; }

        public string? Error { get; set; }
    }
}
