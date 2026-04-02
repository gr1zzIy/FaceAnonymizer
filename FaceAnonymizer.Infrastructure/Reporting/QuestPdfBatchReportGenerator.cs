using FaceAnonymizer.Core.Abstractions;
using FaceAnonymizer.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FaceAnonymizer.Infrastructure.Reporting;

public sealed class QuestPdfBatchReportGenerator : IBatchReportGenerator
{
    public byte[] GeneratePdf(BatchRunSummary summary, IReadOnlyList<BatchFileRow> rows)
    {
        rows ??= Array.Empty<BatchFileRow>();

        var hasMetrics = rows.Any(r => r.MeanIoU is not null || r.Precision is not null);

        var total = rows.Count;
        var facesAvg = total > 0 ? rows.Average(r => r.FacesBefore) : 0d;
        var zeroFaces = rows.Count(r => r.FacesBefore == 0);
        var zeroFacesPct = total > 0 ? 100.0 * zeroFaces / total : 0d;

        var totalMsSorted = rows.Select(r => r.TotalMs).OrderBy(x => x).ToArray();
        var p50 = Percentile(totalMsSorted, 0.50);
        var p90 = Percentile(totalMsSorted, 0.90);
        var p95 = Percentile(totalMsSorted, 0.95);

        var topSlow = rows.OrderByDescending(r => r.TotalMs).Take(10).ToList();
        var worstIou = rows.Where(r => r.MeanIoU is not null).OrderBy(r => r.MeanIoU).Take(10).ToList();

        // Графіки
        var avgBars = BatchCharts.BarTimingsUa(
            summary.AvgDetectMs, summary.AvgAnonymizeMs, summary.AvgRedetectMs, summary.AvgTotalMs);

        var topDetect = BatchCharts.TopFilesBarUa(rows, r => r.DetectMs,
            "Топ-10 найповільніших — Виявлення (Detect)", "мс", 10);
        var topAnon = BatchCharts.TopFilesBarUa(rows, r => r.AnonymizeMs,
            "Топ-10 найповільніших — Анонімізація (Anonymize)", "мс", 10);
        var topRedet = BatchCharts.TopFilesBarUa(rows, r => r.RedetectMs,
            "Топ-10 найповільніших — Повторне виявлення (Redetect)", "мс", 10);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(11f).FontColor(Colors.Grey.Darken4));

                // ─── Заголовок ───
                page.Header().PaddingBottom(12).Column(h =>
                {
                    h.Item().Text("FaceAnonymizer — Звіт пакетної обробки")
                        .Bold().FontSize(20).FontColor(Colors.Black);
                    h.Item().Text($"Ідентифікатор запуску: {summary.RunId}")
                        .FontSize(11).FontColor(Colors.Grey.Darken2);
                    h.Item().Text($"Час (UTC): {summary.UtcStartedAt:yyyy-MM-dd HH:mm:ss} → {summary.UtcFinishedAt:yyyy-MM-dd HH:mm:ss}")
                        .FontSize(11).FontColor(Colors.Grey.Darken2);
                    h.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // ─── Тіло ───
                page.Content().Column(col =>
                {
                    col.Spacing(14);

                    // Параметри запуску
                    col.Item().Element(e => SectionCard(e, "Параметри запуску", body =>
                    {
                        body.Column(c =>
                        {
                            c.Spacing(5);
                            c.Item().Text($"Двигун виявлення: {summary.Engine}").FontSize(11).FontColor(Colors.Black);
                            c.Item().Text($"Метод анонімізації: {summary.Method}   |   Інтенсивність: {summary.Strength:0.00}").FontSize(11).FontColor(Colors.Black);
                            c.Item().Text($"Оцінка повторного виявлення: {(summary.EvaluateRedetection ? "увімкнено" : "вимкнено")}   |   Поріг IoU: {summary.IoUThreshold:0.00}")
                                .FontSize(10).FontColor(Colors.Grey.Darken2);
                        });
                    }));

                    // KPI — файли
                    col.Item().Row(r =>
                    {
                        r.Spacing(10);
                        r.RelativeItem().Element(e => KpiCard(e, "Файлів", summary.TotalFiles.ToString()));
                        r.RelativeItem().Element(e => KpiCard(e, "Успішно", summary.ProcessedOk.ToString()));
                        r.RelativeItem().Element(e => KpiCard(e, "Помилок", summary.Failed.ToString()));
                        r.RelativeItem().Element(e => KpiCard(e, "Загальний час", $"{summary.ElapsedMs:0.#} мс"));
                    });

                    // KPI — тайминги
                    col.Item().Row(r =>
                    {
                        r.Spacing(10);
                        r.RelativeItem().Element(e => KpiCard(e, "Сер. Detect", $"{summary.AvgDetectMs:0.#} мс"));
                        r.RelativeItem().Element(e => KpiCard(e, "Сер. Anonymize", $"{summary.AvgAnonymizeMs:0.#} мс"));
                        r.RelativeItem().Element(e => KpiCard(e, "Сер. Redetect", $"{summary.AvgRedetectMs:0.#} мс"));
                        r.RelativeItem().Element(e => KpiCard(e, "Сер. Total", $"{summary.AvgTotalMs:0.#} мс"));
                    });

                    // KPI — метрики якості
                    if (hasMetrics)
                    {
                        col.Item().Row(r =>
                        {
                            r.Spacing(10);
                            r.RelativeItem().Element(e => KpiCard(e, "Precision", Fmt(summary.AvgPrecision, "0.###")));
                            r.RelativeItem().Element(e => KpiCard(e, "Recall", Fmt(summary.AvgRecall, "0.###")));
                            r.RelativeItem().Element(e => KpiCard(e, "F1", Fmt(summary.AvgF1, "0.###")));
                            r.RelativeItem().Element(e => KpiCard(e, "Mean IoU", Fmt(summary.AvgMeanIoU, "0.###")));
                        });

                        col.Item().Row(r =>
                        {
                            r.Spacing(10);
                            r.RelativeItem().Element(e => KpiCard(e, "SSIM", Fmt(summary.AvgSsim, "0.####")));
                            r.RelativeItem().Element(e => KpiCard(e, "PSNR", summary.AvgPsnrDb is null ? "—" : $"{summary.AvgPsnrDb:0.#} dB"));
                            r.RelativeItem().Element(e => KpiCard(e, "Сер. облич", $"{facesAvg:0.#}"));
                            r.RelativeItem().Element(e => KpiCard(e, "Без облич", $"{zeroFaces} ({zeroFacesPct:0.#}%)"));
                        });
                    }

                    // Підсумок
                    col.Item().Element(e => SectionCard(e, "Підсумок", body =>
                    {
                        body.Column(c =>
                        {
                            c.Spacing(5);
                            c.Item().Text(
                                $"Оброблено {summary.TotalFiles} файлів: {summary.ProcessedOk} успішно, {summary.Failed} з помилками. " +
                                $"Середній час обробки одного файлу — {summary.AvgTotalMs:0.#} мс."
                            ).FontSize(11).FontColor(Colors.Black);

                            c.Item().Text(
                                $"Найбільше часу займає етап «{DominantStage(summary)}». " +
                                $"Перцентилі загального часу: P50 = {p50:0.#} мс, P90 = {p90:0.#} мс, P95 = {p95:0.#} мс."
                            ).FontSize(11).FontColor(Colors.Black);

                            if (hasMetrics)
                            {
                                c.Item().Text(
                                    $"Якість повторного виявлення: Precision {Fmt(summary.AvgPrecision, "0.###")}, " +
                                    $"Recall {Fmt(summary.AvgRecall, "0.###")}, F1 {Fmt(summary.AvgF1, "0.###")}."
                                ).FontSize(11).FontColor(Colors.Black);

                                if (summary.AvgSsim is not null || summary.AvgPsnrDb is not null)
                                    c.Item().Text(
                                        $"Якість зображень: SSIM = {Fmt(summary.AvgSsim, "0.####")}, " +
                                        $"PSNR = {(summary.AvgPsnrDb is null ? "—" : $"{summary.AvgPsnrDb:0.#} dB")}."
                                    ).FontSize(11).FontColor(Colors.Black);
                            }
                        });
                    }));

                    col.Item().PageBreak();
                    // ─── Графік: середні ───
                    col.Item().Text("Середній час по етапах").SemiBold().FontSize(14).FontColor(Colors.Black);
                    col.Item().Image(avgBars);

                    // ─── Графіки: топ-10 ───
                    col.Item().PageBreak();
                    col.Item().Text("Топ-10 найповільніших файлів по етапах").SemiBold().FontSize(14).FontColor(Colors.Black);
                    col.Item().Image(topDetect).FitWidth();

                    col.Item().PageBreak();
                    col.Item().Image(topAnon).FitWidth();

                    col.Item().PageBreak();
                    col.Item().Image(topRedet).FitWidth();

                    // ─── Таблиця: найповільніші ───
                    col.Item().Text("Найповільніші файли (топ-10)").SemiBold().FontSize(14).FontColor(Colors.Black);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(6);
                            c.ConstantColumn(45);
                            c.ConstantColumn(70);
                            c.ConstantColumn(60);
                            c.ConstantColumn(70);
                            c.ConstantColumn(70);
                        });

                        t.Header(h =>
                        {
                            Th(h.Cell(), "Файл");
                            Th(h.Cell(), "Облич");
                            ThR(h.Cell(), "Усього, мс");
                            ThR(h.Cell(), "Detect");
                            ThR(h.Cell(), "Anonymize");
                            ThR(h.Cell(), "Redetect");
                        });

                        for (int i = 0; i < topSlow.Count; i++)
                        {
                            var r = topSlow[i];
                            var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                            Td(t.Cell(), Truncate(ShortFile(r.File), 50), bg);
                            Td(t.Cell(), r.FacesBefore.ToString(), bg);
                            TdR(t.Cell(), $"{r.TotalMs:0.##}", bg);
                            TdR(t.Cell(), $"{r.DetectMs:0.##}", bg);
                            TdR(t.Cell(), $"{r.AnonymizeMs:0.##}", bg);
                            TdR(t.Cell(), $"{r.RedetectMs:0.##}", bg);
                        }
                    });

                    // ─── Таблиця: найгірший IoU ───
                    if (hasMetrics && worstIou.Count > 0)
                    {
                        col.Item().PageBreak();
                        col.Item().Text("Якість повторного виявлення — найгірші результати").SemiBold().FontSize(14).FontColor(Colors.Black);
                        col.Item().Text("Mean IoU показує збіг областей облич до/після анонімізації. Чим ближче до 1 — тим краще.")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(5);
                                c.RelativeColumn(1);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });

                            t.Header(h =>
                            {
                                Th(h.Cell(), "Файл");
                                Th(h.Cell(), "Облич");
                                ThR(h.Cell(), "Усього, мс");
                                ThR(h.Cell(), "Mean IoU");
                            });

                            for (int i = 0; i < worstIou.Count; i++)
                            {
                                var r = worstIou[i];
                                var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                                Td(t.Cell(), ShortFile(r.File), bg);
                                Td(t.Cell(), r.FacesBefore.ToString(), bg);
                                TdR(t.Cell(), $"{r.TotalMs:0.##}", bg);
                                TdR(t.Cell(), Fmt(r.MeanIoU, "0.###"), bg);
                            }
                        });
                    }

                    // ─── Детальна таблиця ───
                    col.Item().PageBreak();
                    col.Item().Text("Детальні результати").SemiBold().FontSize(14).FontColor(Colors.Black);
                    col.Item().Text("Показано до 80 файлів, відсортованих за загальним часом. Повний список — у CSV.")
                        .FontSize(10).FontColor(Colors.Grey.Darken2);

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        t.Header(h =>
                        {
                            Th(h.Cell(), "Файл");
                            Th(h.Cell(), "Облич");
                            ThR(h.Cell(), "Усього, мс");
                            ThR(h.Cell(), "Mean IoU");
                            ThR(h.Cell(), "SSIM");
                            ThR(h.Cell(), "PSNR, dB");
                        });

                        int i = 0;
                        foreach (var r in rows.OrderByDescending(x => x.TotalMs).Take(80))
                        {
                            var bg = i++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                            Td(t.Cell(), ShortFile(r.File), bg);
                            Td(t.Cell(), r.FacesBefore.ToString(), bg);
                            TdR(t.Cell(), $"{r.TotalMs:0.##}", bg);
                            TdR(t.Cell(), Fmt(r.MeanIoU, "0.###"), bg);
                            TdR(t.Cell(), Fmt(r.Ssim, "0.####"), bg);
                            TdR(t.Cell(), Fmt(r.PsnrDb, "0.##"), bg);
                        }
                    });

                    if (rows.Count > 80)
                        col.Item().Text($"(Показано 80 із {rows.Count}. Повний список — у CSV.)")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                });

                // ─── Футер ───
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("FaceAnonymizer").FontSize(9).FontColor(Colors.Grey.Darken2);
                    x.Span($" • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC • Сторінка ").FontSize(9).FontColor(Colors.Grey.Darken2);
                    x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken2);
                    x.Span("/").FontSize(9).FontColor(Colors.Grey.Darken2);
                    x.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            });
        });

        return doc.GeneratePdf();
    }

    // ─── Допоміжні методи ───

    private static string DominantStage(BatchRunSummary s)
    {
        var max = Math.Max(s.AvgDetectMs, Math.Max(s.AvgAnonymizeMs, s.AvgRedetectMs));
        if (Math.Abs(max - s.AvgDetectMs) < 1e-9) return "Виявлення";
        if (Math.Abs(max - s.AvgAnonymizeMs) < 1e-9) return "Анонімізація";
        return "Повторне виявлення";
    }

    private static string Fmt(double? val, string format) =>
        val is null ? "—" : val.Value.ToString(format);

    private static string ShortFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "—";
        try { return Path.GetFileName(path); }
        catch { return path; }
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "—";
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        if (p <= 0) return sorted[0];
        if (p >= 1) return sorted[^1];
        var pos = (sorted.Length - 1) * p;
        var idx = (int)pos;
        var frac = pos - idx;
        return idx + 1 >= sorted.Length ? sorted[idx] : sorted[idx] * (1 - frac) + sorted[idx + 1] * frac;
    }

    // ─── KPI-картка ───

    private static IContainer KpiCard(IContainer c, string title, string value)
    {
        var box = c.Border(1).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten5).Padding(10).CornerRadius(8);
        box.Column(col =>
        {
            col.Item().Text(title).FontSize(9).FontColor(Colors.Grey.Darken2);
            col.Item().Text(value).SemiBold().FontSize(15).FontColor(Colors.Black);
        });
        return box;
    }

    // ─── Секція-картка ───

    private static IContainer SectionCard(IContainer c, string title, Action<IContainer> body)
    {
        var box = c.Border(1).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten5).CornerRadius(10).Padding(14);
        box.Column(col =>
        {
            col.Item().Text(title).SemiBold().FontSize(13).FontColor(Colors.Black);
            col.Item().PaddingTop(8).Element(body);
        });
        return box;
    }

    // ─── Таблиця: заголовки та комірки ───

    private static void Th(IContainer cell, string text) =>
        cell.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(6).PaddingHorizontal(6)
            .Text(text).SemiBold().FontSize(10).FontColor(Colors.Black);

    private static void ThR(IContainer cell, string text) =>
        cell.Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(6).PaddingHorizontal(6).AlignRight()
            .Text(text).SemiBold().FontSize(10).FontColor(Colors.Black);

    private static void Td(IContainer cell, string text, string bg) =>
        cell.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(5).PaddingHorizontal(6)
            .Text(text).FontSize(9.5f).FontColor(Colors.Grey.Darken4).ClampLines(1);

    private static void TdR(IContainer cell, string text, string bg) =>
        cell.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(5).PaddingHorizontal(6).AlignRight()
            .Text(text).FontSize(9.5f).FontColor(Colors.Grey.Darken4);
}