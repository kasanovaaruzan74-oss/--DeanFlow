using DeanFlow.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DeanFlow.Controllers;

public abstract class AppController : Controller
{
    private readonly NotificationService notifications;

    protected AppController(NotificationService notifications) => this.notifications = notifications;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            try { ViewData["UnreadNotifications"] = await notifications.UnreadCountAsync(User.UserId(), HttpContext.RequestAborted); }
            catch { ViewData["UnreadNotifications"] = 0; }
        }
        await next();
    }
}
