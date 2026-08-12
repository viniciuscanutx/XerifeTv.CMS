using Microsoft.Extensions.Options;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Modules.SystemSettings.Interfaces;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SystemSettings;

public sealed class SystemSettingsRepository(IOptions<DBSettings> options)
    : BaseRepository<SystemSettingsEntity>(ECollection.SYSTEM_SETTINGS, options), ISystemSettingsRepository
{
}
