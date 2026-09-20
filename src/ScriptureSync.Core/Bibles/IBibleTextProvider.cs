using ScriptureSync.Core.Models;

namespace ScriptureSync.Core.Bibles;

public interface IBibleTextProvider
{
    Task<PassageText> GetPassageAsync(string translation, PassageReference reference, CancellationToken cancellationToken = default);
}
