namespace STGenerator;

public static class Extensions
{
    private static readonly Random _random = new Random();
    public static void Shuffle<T>(this T[] array, Random rnd)
    {
        int n = array.Length;
        while (n > 1)
        {
            int k = rnd.Next(n--);
            (array[k], array[n]) = (array[n], array[k]);
        }
    }

    public static void ShuffleEveryN<T>(this T[] array, int n, Random rnd)
    {
        if (n <= 0)
            throw new ArgumentOutOfRangeException(nameof(n), "n must be greater than 0.");

        for (int i = 0; i < array.Length; i += n)
        {
            int chunkSize = Math.Min(n, array.Length - i);
            var chunk = array[i..(i + chunkSize)];

            chunk.Shuffle(rnd);

            Array.Copy(chunk, 0, array, i, chunkSize);
        }
    }
}