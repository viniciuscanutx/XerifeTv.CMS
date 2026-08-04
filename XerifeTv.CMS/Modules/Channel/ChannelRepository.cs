using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using XerifeTv.CMS.Shared.Database.MongoDB;
using XerifeTv.CMS.Modules.Channel.Enums;
using XerifeTv.CMS.Modules.Channel.Interfaces;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.Channel.Dtos.Request;
using XerifeTv.CMS.Modules.Common;
using XerifeTv.CMS.Modules.Common.Dtos;

namespace XerifeTv.CMS.Modules.Channel;

public sealed class ChannelRepository(IOptions<DBSettings> options)
  : BaseRepository<ChannelEntity>(ECollection.CHANNELS, options), IChannelRepository
{
    public async Task<PagedList<ChannelEntity>> GetByFilterAsync(GetChannelsByFilterRequestDto dto)
    {
        Expression<Func<ChannelEntity, bool>> filterExpression = dto.Filter switch
        {
            EChannelSearchFilter.TITLE => r =>
              r.Title.Contains(dto.Search, StringComparison.CurrentCultureIgnoreCase) && (!r.Disabled || dto.IsIncludeDisabled),

            EChannelSearchFilter.CATEGORY => r =>
              r.Categories.Any(x =>
                x.Equals(dto.Search.Trim(), StringComparison.CurrentCultureIgnoreCase)) && (!r.Disabled || dto.IsIncludeDisabled),

            _ => r =>
              r.Title.Contains(dto.Search, StringComparison.CurrentCultureIgnoreCase) && (!r.Disabled || dto.IsIncludeDisabled)
        };

        FilterDefinition<ChannelEntity> filter = Builders<ChannelEntity>.Filter.Where(filterExpression);

        var count = await _collection.CountDocumentsAsync(filter);
        var items = await _collection.Find(filter)
          .SortBy(r => r.Title)
          .Skip(dto.LimitResults * (dto.CurrentPage - 1))
          .Limit(dto.LimitResults)
          .ToListAsync();

        var totalPages = (int)Math.Ceiling(count / (decimal)dto.LimitResults);

        return new PagedList<ChannelEntity>(dto.CurrentPage, totalPages, items);
    }

    public async Task<IEnumerable<ItemsByCategory<ChannelEntity>>> GetGroupByCategoryAsync(GetGroupByCategoryRequestDto dto)
    {
        List<ItemsByCategory<ChannelEntity>> result = [];

        var categories = dto.Categories;
        if (categories == null || !categories.Any())
        {
            var allCategoryDocs = await _collection
              .Find(r => !r.Disabled || dto.IsIncludeDisabled)
              .Project(r => r.Categories)
              .ToListAsync();

            categories = allCategoryDocs
              .SelectMany(x => x)
              .Where(x => !string.IsNullOrWhiteSpace(x))
              .Distinct(StringComparer.OrdinalIgnoreCase)
              .ToList();
        }

        foreach (var category in categories)
        {
            var channelsByCategory = await _collection
              .Find(r => r.Categories.Any(x => x.Equals(category, StringComparison.OrdinalIgnoreCase)) && (!r.Disabled || dto.IsIncludeDisabled))
              .SortByDescending(x => x.CreateAt)
              .Skip(dto.LimitResults * (dto.CurrentPage - 1))
              .Limit(dto.LimitResults)
              .ToListAsync();

            if (channelsByCategory.Any())
                result.Add(new ItemsByCategory<ChannelEntity>(category, channelsByCategory));
        }

        if (dto.Categories == null || !dto.Categories.Any())
        {
            var uncategorizedChannels = await _collection
              .Find(r => (r.Categories == null || !r.Categories.Any()) && (!r.Disabled || dto.IsIncludeDisabled))
              .SortByDescending(x => x.CreateAt)
              .Skip(dto.LimitResults * (dto.CurrentPage - 1))
              .Limit(dto.LimitResults)
              .ToListAsync();

            if (uncategorizedChannels.Any())
                result.Add(new ItemsByCategory<ChannelEntity>("Geral", uncategorizedChannels));
        }

        return result;
    }
}