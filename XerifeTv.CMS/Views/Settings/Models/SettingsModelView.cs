using XerifeTv.CMS.Modules.CatalogProvider.Dtos.Response;
using XerifeTv.CMS.Modules.Integrations.Webhook.Dtos.Response;
using XerifeTv.CMS.Modules.LinkTemplate.Dtos.Response;
using XerifeTv.CMS.Modules.Media.Delivery.Dtos.Response;
using XerifeTv.CMS.Modules.User.Dtos.Response;

namespace XerifeTv.CMS.Views.Settings.Models;

public sealed record SettingsModelView(
    GetUserResponseDto UserSettingModel,
    IEnumerable<GetWebhookResponseDto> WebHooks,
    IEnumerable<GetMediaDeliveryProfileResponseDto> MediaDeliveryProfiles,
    IEnumerable<GetLinkTemplateResponseDto> LinkTemplates,
    IEnumerable<GetCatalogProviderResponseDto> CatalogProviders);
