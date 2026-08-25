using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.Services;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class DrugSuggestionService(AppDbContext db) : IDrugSuggestionService
{
    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(string? term)
    {
        var query = db.PrescriptionItems.AsQueryable();

        // Case-insensitive Contains (not StartsWith), matching this
        // codebase's established default for free-text lookup -- see
        // IDrugSuggestionService's remarks and docs/implementation-progress.md
        // Step 14 for why this was resolved rather than escalated.
        if (!string.IsNullOrWhiteSpace(term))
        {
            var lowerTerm = term.ToLower();
            query = query.Where(i => i.DrugName.ToLower().Contains(lowerTerm));
        }

        return await query
            .Select(i => i.DrugName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();
    }
}
