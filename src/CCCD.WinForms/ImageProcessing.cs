using System;
using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace CCCD.WinForms;

public static class ImageProcessing
{
    private const int TargetWidth = 856;
    private const int TargetHeight = 540;

    public static Bitmap AutoCropCard(Bitmap input)
    {
        using var source = BitmapConverter.ToMat(input);
        using var resized = ResizeIfNeeded(source);
        using var gray = new Mat();
        using var blurred = new Mat();
        using var edges = new Mat();

        Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        Cv2.Canny(blurred, edges, 50, 150);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        Point2f[]? cardContour = null;
        var maxArea = 0.0;

        foreach (var contour in contours)
        {
            var peri = Cv2.ArcLength(contour, true);
            var approx = Cv2.ApproxPolyDP(contour, 0.02 * peri, true);
            if (approx.Length != 4)
            {
                continue;
            }

            var area = Cv2.ContourArea(approx);
            if (area <= maxArea)
            {
                continue;
            }

            maxArea = area;
            cardContour = Array.ConvertAll(approx, point => new Point2f(point.X, point.Y));
        }

        if (cardContour == null)
        {
            return (Bitmap)input.Clone();
        }

        var ordered = OrderPoints(cardContour);
        using var warped = WarpToCard(resized, ordered);
        return BitmapConverter.ToBitmap(warped);
    }

    public static Bitmap CombineSideBySide(Bitmap front, Bitmap back)
    {
        var gap = 24;
        var height = Math.Max(front.Height, back.Height);
        var totalWidth = front.Width + back.Width + gap;

        var combined = new Bitmap(totalWidth, height);
        using var graphics = Graphics.FromImage(combined);
        graphics.Clear(Color.White);
        graphics.DrawImage(front, new Rectangle(0, 0, front.Width, front.Height));
        graphics.DrawImage(back, new Rectangle(front.Width + gap, 0, back.Width, back.Height));

        return combined;
    }

    public static Size ScaleToFit(Image image, Size bounds)
    {
        var ratioX = (double)bounds.Width / image.Width;
        var ratioY = (double)bounds.Height / image.Height;
        var ratio = Math.Min(ratioX, ratioY);
        return new Size((int)(image.Width * ratio), (int)(image.Height * ratio));
    }

    private static Mat WarpToCard(Mat source, Point2f[] points)
    {
        var widthA = Distance(points[2], points[3]);
        var widthB = Distance(points[1], points[0]);
        var maxWidth = Math.Max(widthA, widthB);

        var heightA = Distance(points[1], points[2]);
        var heightB = Distance(points[0], points[3]);
        var maxHeight = Math.Max(heightA, heightB);

        var target = new Size(TargetWidth, TargetHeight);
        var src = points;
        var dst = new[]
        {
            new Point2f(0, 0),
            new Point2f(target.Width - 1, 0),
            new Point2f(target.Width - 1, target.Height - 1),
            new Point2f(0, target.Height - 1)
        };

        using var matrix = Cv2.GetPerspectiveTransform(src, dst);
        var warped = new Mat();
        Cv2.WarpPerspective(source, warped, matrix, target);
        return warped;
    }

    private static Point2f[] OrderPoints(Point2f[] points)
    {
        var ordered = new Point2f[4];
        var sum = new double[4];
        var diff = new double[4];

        for (var i = 0; i < points.Length; i++)
        {
            sum[i] = points[i].X + points[i].Y;
            diff[i] = points[i].Y - points[i].X;
        }

        ordered[0] = points[Array.IndexOf(sum, Min(sum))];
        ordered[2] = points[Array.IndexOf(sum, Max(sum))];
        ordered[1] = points[Array.IndexOf(diff, Min(diff))];
        ordered[3] = points[Array.IndexOf(diff, Max(diff))];

        return ordered;
    }

    private static double Min(double[] values) => values.Length == 0 ? 0 : Math.Min(Math.Min(values[0], values[1]), Math.Min(values[2], values[3]));

    private static double Max(double[] values) => values.Length == 0 ? 0 : Math.Max(Math.Max(values[0], values[1]), Math.Max(values[2], values[3]));

    private static double Distance(Point2f a, Point2f b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Mat ResizeIfNeeded(Mat input)
    {
        var maxSide = Math.Max(input.Width, input.Height);
        if (maxSide <= 1600)
        {
            return input.Clone();
        }

        var scale = 1600.0 / maxSide;
        var resized = new Mat();
        Cv2.Resize(input, resized, new Size(), scale, scale, InterpolationFlags.Area);
        return resized;
    }
}
