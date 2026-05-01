using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Descriptions.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Descriptions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.Descriptions
{
    public interface IApartmentAiDescriptionService
    {
        Task<ApartmentAiDescriptionDto?> GetActiveDescriptionAsync(int apartmentId, string culture, CancellationToken cancellationToken = default);
    }

    public class ApartmentAiDescriptionService : IApartmentAiDescriptionService
    {
        private readonly IDbContextFactory<RappDescriptionsDbContext> _dbContextFactory;

        public ApartmentAiDescriptionService(IDbContextFactory<RappDescriptionsDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<ApartmentAiDescriptionDto?> GetActiveDescriptionAsync(int apartmentId, string culture, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // KROK 1: Znajdz aktywny zestaw dla apartamentu
            var activeSet = await dbContext.DescriptionSets
                .AsNoTracking()
                .Where(s => s.ApartmentId == apartmentId && s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeSet == null) return null;

            // KROK 2: Sprawdz jaki wariant jest przypisany do rentoomPl (bazujac na source language)
            var activeVariantMapping = await dbContext.Variants
                .AsNoTracking()
                .Include(v => v.Channels)
                .Where(v => v.DescriptionSetId == activeSet.Id && v.TranslationStatus == "source")
                .Where(v => v.Channels.Any(c => c.ChannelCode == "rentoomPl" && c.IsEnabled))
                .FirstOrDefaultAsync(cancellationToken);

            if (activeVariantMapping == null) return null;

            string variantType = activeVariantMapping.VariantType;

            // KROK 3: Pobierz konkretny wariant w wybranym jezyku
            // Normalizujemy culture (np. pl-PL -> pl)
            string langCode = culture.Split('-')[0].ToLowerInvariant();

            var targetVariant = await dbContext.Variants
                .AsNoTracking()
                .Where(v => v.DescriptionSetId == activeSet.Id && 
                            v.VariantType == variantType && 
                            v.LanguageCode == langCode)
                .FirstOrDefaultAsync(cancellationToken);

            // Jesli nie ma tlumaczenia w tym jezyku, probujemy pobrac source (zazwyczaj pl)
            if (targetVariant == null)
            {
                targetVariant = activeVariantMapping;
            }

            // Pobieramy dodatki (FAQ, Highlights, SEO) dla tego samego jezyka
            var faqs = await dbContext.Faqs
                .AsNoTracking()
                .Where(f => f.DescriptionSetId == activeSet.Id && f.LanguageCode == langCode)
                .OrderBy(f => f.SortOrder)
                .ToListAsync(cancellationToken);

            var highlights = await dbContext.Highlights
                .AsNoTracking()
                .Where(h => h.DescriptionSetId == activeSet.Id && h.LanguageCode == langCode)
                .OrderBy(h => h.SortOrder)
                .ToListAsync(cancellationToken);

            var seoPhrases = await dbContext.SeoPhrases
                .AsNoTracking()
                .Where(p => p.DescriptionSetId == activeSet.Id && p.LanguageCode == langCode)
                .OrderBy(p => p.SortOrder)
                .ToListAsync(cancellationToken);

            // Skladamy DTO
            return new ApartmentAiDescriptionDto
            {
                H1 = targetVariant.H1,
                ShortDescription = targetVariant.ShortDescription,
                MainDescription = targetVariant.MainDescription,
                MetaTitle = targetVariant.MetaTitle,
                MetaDescription = targetVariant.MetaDescription,
                VariantType = variantType,
                LanguageCode = langCode,
                Faqs = faqs.Select(f => new AiFaqItemDto { Question = f.Question, Answer = f.Answer }).ToList(),
                Highlights = highlights.Select(h => h.Text).ToList(),
                SeoPhrases = seoPhrases.Select(p => p.Phrase).ToList()
            };
        }
    }
}
