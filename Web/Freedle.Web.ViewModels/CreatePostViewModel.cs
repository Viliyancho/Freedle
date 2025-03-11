using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Web.ViewModels
{
    public class CreatePostViewModel
    {
        [Required]
        [StringLength(1000, MinimumLength = 3)]
        [Display(Name = "Add content...")]
        public string Content { get; set; }

        [Display(Name = "Add a photo")]
        public string ImageURL { get; set; } // Отговаря на Posts - Title
    }
}
