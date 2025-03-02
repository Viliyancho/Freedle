using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Freedle.Data.Models
{
    public class Page
    {
        public Page()
        {
            this.Posts = new HashSet<Post>();
        }

        public int Id { get; set; }

        public ICollection<Post> Posts { get; set; }

        [ForeignKey("PageAdmin")]
        public string PageAdminId { get; set; }

        public ApplicationUser PageAdmin { get; set; }
    }
}
