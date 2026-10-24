using Microsoft.Extensions.Options;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.Abstractions.Repositories;
using XerifeTv.CMS.Shared.Database.MongoDB;

namespace XerifeTv.CMS.Modules.SiteProfile;

public interface ISiteProfileImageRepository : IBaseRepository<SiteProfileImageEntity>;

public sealed class SiteProfileImageRepository(IOptions<DBSettings> options)
    : BaseRepository<SiteProfileImageEntity>(ECollection.SITE_PROFILE_IMAGES, options), ISiteProfileImageRepository;
