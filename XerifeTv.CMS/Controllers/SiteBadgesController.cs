using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.SiteBadge;
using XerifeTv.CMS.Shared.Helpers;
namespace XerifeTv.CMS.Controllers;
[Authorize(Roles = "admin")]
public sealed class SiteBadgesController(ISiteBadgeService badges) : Controller
{
    public async Task<IActionResult> Index() => View(await badges.GetAllAsync());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SaveSiteBadgeRequest request)
    {
        try
        {
            if (!ModelState.IsValid) throw new ArgumentException("Informe um nome e uma cor válidos.");
            await badges.SaveAsync(request);
            TempData["Notification"] = MessageViewHelper.SuccessJson("Badge salvo com sucesso.");
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        {
            TempData["Notification"] = MessageViewHelper.ErrorJson(ex.Message);
        }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await badges.DeleteAsync(id);
            TempData["Notification"] = MessageViewHelper.SuccessJson("Badge excluído. Ele não será mais exibido nos perfis.");
        }
        catch (KeyNotFoundException ex)
        {
            TempData["Notification"] = MessageViewHelper.ErrorJson(ex.Message);
        }
        return RedirectToAction(nameof(Index));
    }
}
