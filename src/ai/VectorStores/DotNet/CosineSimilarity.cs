namespace VectorStores.DotNet;

public static class CosineSimilarity
{
    public static double Calculate(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
            return 0;

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        if (leftNorm == 0 || rightNorm == 0)
            return 0;

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}