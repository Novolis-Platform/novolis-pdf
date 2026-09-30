using Novolis.Pdf.Documents;

namespace Novolis.Pdf.Unit;

public sealed class PdfDocumentStateStoreTests
{
    [Test]
    public async Task PersistsRecentDocumentAndPositionAtomically()
    {
        var root = Path.Combine(Path.GetTempPath(), $"novolis-pdf-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonPdfDocumentStateStore(root);
            var recent = await store.RememberAsync(new PdfRecentDocument(
                "id-1",
                "sample.pdf",
                "content://sample",
                DateTimeOffset.UtcNow));
            await store.SavePositionAsync(new PdfReadingPosition("id-1", 4, 1.5, 90));

            var reopened = new JsonPdfDocumentStateStore(root);
            var loaded = await reopened.LoadRecentAsync();
            var position = await reopened.LoadPositionAsync("id-1");

            await Assert.That(recent.Count).IsEqualTo(1);
            await Assert.That(loaded.Single().DisplayName).IsEqualTo("sample.pdf");
            await Assert.That(position?.PageIndex).IsEqualTo(4);
            await Assert.That(position?.Rotation).IsEqualTo(90);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
