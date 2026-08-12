using Microsoft.Extensions.Options;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.Activity.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.Activity;

public sealed class ActivityLogRepository(IOptions<DBSettings> options)
    : BaseRepository<ActivityLogEntity>(ECollection.ACTIVITIES, options), IActivityLogRepository
{
}
