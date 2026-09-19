using ScriptureSync.Core.Bibles;
using ScriptureSync.Core.Models;

namespace ScriptureSync.ProPresenter;

public sealed class ProPresenterBibleProvider(string root) : IBibleTextProvider
{
    public Task<PassageText> GetPassageAsync(string translation, PassageReference reference, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new UsxBibleReader().Read(new ProPresenterBibleCatalog(root).Find(translation), reference);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, cancellationToken);
}
