namespace Freedle.Web.Controllers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Freedle.Data;
    using Freedle.Data.Models;
    using Freedle.Web.ViewModels;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    public class HomeController : BaseController
    {

        private readonly ApplicationDbContext dbContext;
        private readonly UserManager<ApplicationUser> userManager;

        public HomeController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            this.dbContext = dbContext;
            this.userManager = userManager;
        }


        public async Task<IActionResult> Index()
        {
            var currentUser = await this.userManager.GetUserAsync(User);
            string currentUserId = currentUser?.Id;

            // Взимаме последните 20 поста
            var posts = this.dbContext.Posts
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedOn)
                .Take(20)
                .Select(p => new PostViewModel
                {
                    Id = p.Id,
                    Content = p.Content,
                    ImageUrl = p.ImageURL,
                    CreatedOn = p.CreatedOn.ToString("f"),
                    AuthorId = p.User.Id,
                    AuthorName = $"{p.User.FirstName} {p.User.LastName}",
                    AuthorProfilePictureUrl = p.User.ProfilePictureURL,
                    LikeCount = p.LikeCount,
                    IsLikedByCurrentUser = p.Likes.Any(l => l.UserId == currentUserId),
                    Comments = p.Comments.Select(c => new CommentViewModel
                    {
                        Id = c.Id,
                        AuthorId = c.Author.Id,
                        AuthorName = $"{c.Author.FirstName} {c.Author.LastName}",
                        AuthorProfilePictureUrl = c.Author.ProfilePictureURL,
                        CommentText = c.CommentText,
                        PostedOn = c.PostedOn.ToString("f"),
                    }).ToList(),
                })
                .ToList();

            // Препоръчани потребители (случайни 5)
            var suggestedUsers = this.dbContext.Users
                .Where(u => u.Id != currentUserId)
                .OrderBy(r => Guid.NewGuid()) // Random
                .Take(5)
                .Select(u => new SuggestedUserViewModel
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    ProfilePictureUrl = u.ProfilePictureURL,
                    IsFollowedByCurrentUser = u.Followers.Any(f => f.FollowerId == currentUserId),
                })
                .ToList();

            var model = new NewsFeedViewModel
            {
                Posts = posts,
                SuggestedUsers = suggestedUsers,
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleLike(int postId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return this.Redirect("/Identity/Account/Login"); // Ако не е логнат, пращаме към логин
            }

            var post = dbContext.Posts.Include(p => p.Likes).FirstOrDefault(p => p.Id == postId);
            if (post == null)
            {
                return NotFound();
            }

            var existingLike = dbContext.UserLikes.FirstOrDefault(l => l.PostId == postId && l.UserId == userId);

            if (existingLike != null)
            {
                // Премахва лайка, ако вече е натиснат
                post.Likes.Remove(existingLike);
                dbContext.UserLikes.Remove(existingLike);
                post.LikeCount--;
            }
            else
            {
                // Добавя нов лайк
                var like = new UserLike { PostId = postId, UserId = userId };
                post.Likes.Add(like);
                dbContext.UserLikes.Add(like);
                post.LikeCount++;
            }

            dbContext.SaveChanges();

            return RedirectToAction("Index", "Home"); // Презарежда страницата
        }

        public IActionResult MessagesDemo()
        {
            return this.View();
        }

        public async Task<IActionResult> MyProfile()
        {
            var currentUser = await this.userManager.GetUserAsync(User);

            // Ако потребителят не е логнат, пренасочваме към страницата за вход
            if (currentUser == null)
            {
                return this.Redirect("/Identity/Account/Login");
            }

            var followerCount = this.dbContext.UserFollowers
        .Where(f => f.UserId == currentUser.Id && f.UnfollowedDate == null)
        .Count();

            // Броят на хората, които потребителят следва: броим колко потребители текущият потребител следва
            var followingCount = this.dbContext.UserFollowers
                .Where(f => f.FollowerId == currentUser.Id && f.UnfollowedDate == null)
                .Count();

            // Вземаме допълнителна информация от потребителския профил (например, име, дата на раждане, пол и т.н.)
            var userProfileViewModel = new UserProfileViewModel
            {
                Username = currentUser.UserName,
                FirstName = currentUser.FirstName,
                LastName = currentUser.LastName,
                ProfilePictureUrl = currentUser.ProfilePictureURL,
                Gender = currentUser.Gender,
                BirthDay = currentUser.BirthDay,
                Description = currentUser.Description,
                City = currentUser.City,
                Country = currentUser.Country,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
            };

            // Връщаме данните към изгледа (може да го използвате с изглед с име "MyProfile")
            return View(userProfileViewModel);
        }

        // GET метод, който показва формата за създаване на пост
        public IActionResult CreatePost()
        {
            return View();  
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(CreatePostViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await this.userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return this.Redirect("/Identity/Account/Login");
                }

                string imagePath = null;

                // Проверяваме дали потребителят е качил файл
                if (model.ImageURL != null && model.ImageURL.Length > 0)
                {
                    // Генерираме уникално име за файла
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageURL.FileName);
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

                    // Създаваме директорията ако не съществува
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string filePath = Path.Combine(uploadsFolder, fileName);

                    // Запазваме файла в wwwroot/images
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageURL.CopyToAsync(fileStream);
                    }

                    imagePath = "/images/" + fileName;
                }

                var newPost = new Post
                {
                    Content = model.Content,
                    ImageURL = imagePath,
                    CreatedOn = DateTime.UtcNow,
                    UserId = currentUser.Id,
                };

                currentUser.Posts.Add(newPost);
                await this.dbContext.SaveChangesAsync();

                return RedirectToAction("MyProfile", "Home");
            }

            return this.Redirect("Views/Shared/Error");
        }

        public IActionResult Search()
        {
            return this.View();
        }

        public IActionResult Messages()
        {
            return this.View();
        }

        public IActionResult Privacy()
        {
            return this.View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(
                new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
