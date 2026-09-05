using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using XerifeTv.CMS.Modules.SiteUser.Interfaces;

namespace XerifeTv.CMS.Controllers.ContentAPI;

[ApiController]
[EnableCors("AuthApi")]
[Authorize(AuthenticationSchemes = "SiteJwt")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class SiteAccountController(ISiteUserRepository users) : Controller
{
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CurrentUserId) || await users.GetAsync(CurrentUserId) is null)
            {
                context.Result = Unauthorized(new { message = "Conta não encontrada." });
                return;
            }
            var executed = await next();
            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                executed.Result = HandleException(executed.Exception);
                executed.ExceptionHandled = true;
            }
        }
        catch (Exception exception)
        {
            context.Result = HandleException(exception);
        }
    }

    private IActionResult HandleException(Exception exception)
    {
        if (exception is ArgumentException)
            return BadRequest(new { message = exception.Message });
        if (exception is KeyNotFoundException)
            return NotFound(new { message = exception.Message });
        HttpContext.RequestServices.GetRequiredService<ILogger<SiteAccountController>>()
            .LogError(exception, "Falha na operação da conta do site");
        return StatusCode(500, new { message = "Não foi possível concluir a operação. Tente novamente." });
    }
}
