using System.Text.RegularExpressions;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;
namespace XerifeTv.CMS.Modules.SiteBadge;
public interface ISiteBadgeService
{
    Task<List<SiteBadgeResponse>> GetAllAsync();
    Task<List<SiteBadgeResponse>> GetAssignedAsync(IEnumerable<string> ids);
    Task SaveAsync(SaveSiteBadgeRequest request);
    Task DeleteAsync(string id);
    Task AssignAsync(AssignSiteBadgesRequest request);
    Task SelectAsync(string userId, string? badgeId);
}
public sealed class SiteBadgeService(ISiteBadgeRepository badges, ISiteUserRepository users) : ISiteBadgeService
{
    public async Task<List<SiteBadgeResponse>> GetAllAsync()
        => (await badges.GetAllAsync()).Select(SiteBadgeResponse.FromEntity).ToList();
    public async Task<List<SiteBadgeResponse>> GetAssignedAsync(IEnumerable<string> ids)
        => (await badges.GetByIdsAsync(ids)).Select(SiteBadgeResponse.FromEntity).ToList();
    public async Task SaveAsync(SaveSiteBadgeRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 40 || !Regex.IsMatch(request.Color ?? "", "^#[0-9a-fA-F]{6}$"))
            throw new ArgumentException("Informe um nome com até 40 caracteres e uma cor válida.");
        if ((await badges.GetAllAsync()).Any(x => x.Id != request.Id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Já existe um badge com esse nome.");
        var badge = string.IsNullOrEmpty(request.Id) ? new SiteBadgeEntity()
            : await badges.GetAsync(request.Id) ?? throw new KeyNotFoundException("Badge não encontrado.");
        badge.Name = name;
        badge.Color = request.Color!.ToLowerInvariant();
        if (string.IsNullOrEmpty(request.Id)) await badges.CreateAsync(badge);
        else await badges.UpdateAsync(badge);
    }
    public async Task DeleteAsync(string id)
    {
        if (await badges.GetAsync(id) is null) throw new KeyNotFoundException("Badge não encontrado.");
        await badges.DeleteAsync(id);
    }
    public async Task AssignAsync(AssignSiteBadgesRequest request)
    {
        var ids = (request.BadgeIds ?? []).Distinct().ToList();
        if (ids.Count > 100 || (await badges.GetByIdsAsync(ids)).Count != ids.Count)
            throw new ArgumentException("Selecione apenas badges existentes (até 100). ");
        if (!await users.AssignBadgesAsync(request.UserId, ids))
            throw new KeyNotFoundException("Usuário não encontrado.");
    }
    public async Task SelectAsync(string userId, string? badgeId)
    {
        if (badgeId is not null && await badges.GetAsync(badgeId) is null)
            throw new ArgumentException("Esse badge não está disponível.");
        if (!await users.SelectBadgeAsync(userId, badgeId))
            throw new ArgumentException("Você só pode exibir um badge atribuído ao seu perfil.");
    }
}
