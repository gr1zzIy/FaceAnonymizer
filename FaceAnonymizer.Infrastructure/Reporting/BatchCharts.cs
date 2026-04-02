using FaceAnonymizer.Core.Models;
using ScottPlot;

namespace FaceAnonymizer.Infrastructure.Reporting;

public static class BatchCharts
{
    /// <summary>Стовпчикова діаграма середнього часу по етапах пайплайну.</summary>
    public static byte[] BarTimingsUa(double detect, double anonymize, double redetect, double total)
    {
        var plt = new Plot();

        double[] values = [detect, anonymize, redetect, total];
        string[] labels = ["Виявлення", "Анонімізація", "Повт. виявлення", "Загалом"];
        double[] xs = [0, 1, 2, 3];

        plt.Add.Bars(xs, values);
        plt.Axes.Bottom.SetTicks(xs, labels);
        plt.Axes.Bottom.TickLabelStyle.Rotation = 0;
        plt.Axes.Bottom.TickLabelStyle.FontSize = 24;
        plt.Axes.Left.TickLabelStyle.FontSize = 22;
        plt.Axes.Bottom.MajorTickStyle.Length = 0;

        plt.Title("Середній час по етапах (мс)", 28);
        plt.YLabel("мс");
        plt.Axes.Left.Label.FontSize = 22;

        double yMax = Math.Max(1, values.Max() * 1.15);
        plt.Axes.SetLimitsY(0, yMax);

        return plt.GetImageBytes(1600, 700);
    }

    /// <summary>Топ-N найповільніших файлів для конкретного етапу.</summary>
    public static byte[] TopFilesBarUa(
        IReadOnlyList<BatchFileRow> rows,
        Func<BatchFileRow, double> selectorMs,
        string title,
        string unit = "мс",
        int topN = 10)
    {
        if (rows.Count == 0)
        {
            var empty = new Plot();
            empty.Title(title + " (немає даних)", 26);
            return empty.GetImageBytes(1800, 900);
        }

        topN = Math.Clamp(topN, 5, 25);

        var top = rows
            .Select(r => new { Row = r, Value = Math.Max(0, selectorMs(r)) })
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .Take(topN)
            .ToList();

        var plt = new Plot();

        if (top.Count == 0)
        {
            plt.Title(title + " (немає даних)", 26);
            return plt.GetImageBytes(1800, 900);
        }

        double[] values = top.Select(x => x.Value).ToArray();
        double[] xs = Enumerable.Range(0, top.Count).Select(i => (double)i).ToArray();
        string[] labels = top.Select(x => ShortenFileName(x.Row.File, 26)).ToArray();

        plt.Add.Bars(xs, values);
        plt.Axes.Bottom.SetTicks(xs, labels);

        plt.Axes.Bottom.TickLabelStyle.Rotation = 0;
        plt.Axes.Bottom.TickLabelStyle.FontSize = 20;
        plt.Axes.Left.TickLabelStyle.FontSize = 22;
        plt.Axes.Bottom.MajorTickStyle.Length = 0;

        plt.Title(title, 26);
        plt.YLabel(unit);
        plt.Axes.Left.Label.FontSize = 22;

        double yMax = Math.Max(1, values.Max() * 1.15);
        plt.Axes.SetLimitsY(0, yMax);

        return plt.GetImageBytes(1800, 900);
    }

    private static string ShortenFileName(string path, int max)
    {
        string name;
        try { name = Path.GetFileName(path); }
        catch { name = path; }

        if (string.IsNullOrWhiteSpace(name)) return "—";
        return name.Length <= max ? name : name[..(max - 1)] + "…";
    }
}