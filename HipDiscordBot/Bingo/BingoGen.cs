using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace HipDiscordBot.Bingo;

public class BingoGen
{
    private static readonly string FolderPath = "./BingoPictures";

    private static readonly int SquareSize = 200;
    private static readonly int BorderSize = 5;

    private static readonly Lock Lock = new();
    private static readonly Queue<TaskCompletionSource> Queue = new();
    private static bool _processing = false;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="side"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException">Если таска была отменена.</exception>
    public static async Task<MemoryStream?> GenAsync(int side, CancellationToken cancellationToken)
    {
        // Странно, что эта синхронная проверка тут, а остальные там.
        if (!Directory.Exists(FolderPath))
            return null;

        // Наверное, можно также поставить лимит на количество итемов в очереди, но мне впадлу.

        TaskCompletionSource tsc = new(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (Lock)
        {
            if (_processing)
            {
                Queue.Enqueue(tsc);
            }
            else
            {
                tsc.SetResult();
                _processing = true;
            }
        }

        try
        {
            await tsc.Task.WaitAsync(cancellationToken);

            return await GenImageAsync(side, cancellationToken);
        }
        finally
        {
            lock (Lock)
            {
                if (Queue.TryDequeue(out TaskCompletionSource? nextTsc))
                {
                    nextTsc.SetResult();
                }
                else
                {
                    _processing = false;
                }
            }
        }
    }

    private static async Task<MemoryStream?> GenImageAsync(int side, CancellationToken cancellationToken)
    {
        List<string> allFiles = Directory.GetFiles(FolderPath).ToList();

        if (allFiles.Count < side * side)
            return null;

        // граница есть между каждым элементом а также с обеих сторон изображения

        int imageSize = side * SquareSize + (side - 1 + 2) * BorderSize;

        Image<Rgba32> resultImage = new(imageSize, imageSize, new Rgba32(0, 0, 0, 255));

        int x = BorderSize;
        int y = BorderSize;

        for (int i = 0; i < side * side; i++)
        {
            int next = Random.Shared.Next(0, allFiles.Count);
            string file = allFiles[next];
            allFiles.RemoveAt(next);

            using Image image = await Image.LoadAsync(file, cancellationToken);

            if (image.Width != SquareSize || image.Width != SquareSize)
            {
                image.Mutate(ctx => ctx.Resize(new Size(SquareSize)));
            }

            // тут кстати пишет что опасти 0-1, а в процессоре не пишет.
            resultImage.Mutate(ctx => ctx.DrawImage(image, new Point(x, y), 1));
            // result.Mutate(new DrawImageProcessor(image, new Point(x, y), PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.SrcOver, 1));

            if ((i + 1) % side == 0)
            {
                x = BorderSize;
                y += BorderSize;
                y += SquareSize;
            }
            else
            {
                x += BorderSize;
                x += SquareSize;
            }
        }

        MemoryStream result = new();

        await resultImage.SaveAsWebpAsync(result, cancellationToken: cancellationToken);

        result.Seek(0, SeekOrigin.Begin);

        return result;
    }
}