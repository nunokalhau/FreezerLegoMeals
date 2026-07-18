namespace Services.DotNet.Contracts
{
    public sealed class RecipeInfoResponse
    {
        public required int Id { get; init; }

        public required string Name { get; init; }

        public required int Servings { get; init; }

        public required int TimeToPrepare { get; init; }
    }
}
