using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
namespace Tourist_Project_MVC.Controllers.Middlewares
{
    public class UserExistsMiddleware
    {
        private readonly RequestDelegate _next;

        public UserExistsMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        
        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await userManager.FindByIdAsync(userId);

                    if (user == null)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        var errorResponse = new
                        {
                            success = false,
                            message = "User no longer exists in the system."
                        };
                        await context.Response.WriteAsJsonAsync(errorResponse);
                        return;
                    }
                }
            }
            await _next(context);
        }
    }
}
