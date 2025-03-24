using Freedle.Data.Models;
using Freedle.Data;
using Freedle.Web.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Freedle.Web.Controllers
{
    public class SearchController : BaseController
    {
        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<ChatHub> hubContext;

        public SearchController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hubContext)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(new { users = new List<object>() });
            }

            var currentUser = await userManager.GetUserAsync(User);

            var users = await this.dbContext.Users
                .Where(u => u.UserName.Contains(query) && (currentUser == null || u.Id != currentUser.Id)) // Филтрираме текущия потребител
                .Select(u => new
                {
                    id = u.Id,
                    username = u.UserName,
                    profilePicture = u.ProfilePictureURL ?? "/images/default-avatar.png",
                })
                .ToListAsync();


            return Json(new { users });
        }
    }
}
