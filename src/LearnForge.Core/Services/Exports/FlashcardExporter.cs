using System.Diagnostics;
using System.ComponentModel;

namespace LearnForge.Core;

public static class FlashcardExporter
{
    private static readonly SemaphoreSlim Slots = new(2);
    public static async Task<FlashcardExportResult> Export(Pack pack, FlashcardExportOptions options,
        string? python = null, CancellationToken cancellation = default)
    {
        if (!Enum.IsDefined(options.Format) || !Enum.IsDefined(options.Paper) || !Enum.IsDefined(options.Mode) || !Enum.IsDefined(options.Binding) ||
            !double.IsFinite(options.OffsetX) || !double.IsFinite(options.OffsetY) || Math.Abs(options.OffsetX) > 5 || Math.Abs(options.OffsetY) > 5)
            throw new FlashcardExportException("Invalid export settings. Alignment offsets must be between -5 and 5 mm.");
        var decks = pack.Decks.Where(d => options.Deck is null || d.Id == options.Deck).Select(d => d with
        {
            Notes = d.Notes.Where(n => (options.Objective is null || n.ObjectiveIds.Contains(options.Objective)) &&
                (options.Tag is null || (n.Tags ?? []).Contains(options.Tag))).ToArray()
        }).Where(d => d.Notes.Length > 0).ToArray();
        if (decks.Length == 0) throw new FlashcardExportException("No flashcards match the selected filters.");
        if (!await Slots.WaitAsync(0, cancellation)) throw new FlashcardExportException("Two exports are running. Try again shortly.", true);
        var directory = Path.Combine(Path.GetTempPath(), "learnforge-cards-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var suffix = options.Format switch { FlashcardExportFormat.Apkg => "apkg", FlashcardExportFormat.Text => "zip", _ => "pdf" };
            var output = Path.Combine(directory, "cards." + suffix);
            var script = Path.Combine(AppContext.BaseDirectory, "flashcards", "export.py");
            var start = new ProcessStartInfo(python ?? Environment.GetEnvironmentVariable("LEARNFORGE_EXPORT_PYTHON") ?? "python3")
                { RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
            start.ArgumentList.Add(script); start.ArgumentList.Add(output);
            using var process = Process.Start(start) ?? throw new FlashcardExportException("Could not start the flashcard exporter.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(120));
            try
            {
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var selected = pack with { FlashcardDecks = decks };
                try
                {
                    await process.StandardInput.WriteAsync(Json.Write(new FlashcardExportPayload(pack.Id, pack.Title, pack.Version, decks,
                        Flashcards.Expand(selected), options)).AsMemory(), timeout.Token);
                    process.StandardInput.Close();
                }
                catch (IOException)
                {
                    // The exporter exited before reading its input; its exit code and error output explain why.
                    try { process.StandardInput.Close(); } catch (IOException) { }
                }
                await process.WaitForExitAsync(timeout.Token);
                var error = await errors; await stdout;
                if (process.ExitCode != 0) throw new FlashcardExportException(error.StartsWith("EXPORT_ERROR:", StringComparison.Ordinal)
                    ? error[13..].Trim() : "Flashcard export failed. Check the export Python environment and installed dependencies.");
                if (new FileInfo(output).Length > 50_000_000) throw new FlashcardExportException("Export exceeds 50 MB. Select a smaller deck.");
                return new(await File.ReadAllBytesAsync(output, cancellation), options.Format switch
                    { FlashcardExportFormat.Pdf => "application/pdf", FlashcardExportFormat.Text => "application/zip", _ => "application/octet-stream" },
                    $"{pack.Id}-{pack.Version}-flashcards.{suffix}");
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                if (cancellation.IsCancellationRequested) throw;
                throw new FlashcardExportException("Export timed out. Select a smaller deck and retry.");
            }
        }
        catch (Win32Exception) { throw new FlashcardExportException("Export Python is unavailable. Run the flashcard setup instructions."); }
        finally
        {
            // A leftover temporary directory must not replace the export result or error.
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            finally { Slots.Release(); }
        }
    }
}
