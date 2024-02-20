using Microsoft.EntityFrameworkCore;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public class SurveyContext:DbContext
    {
        public SurveyContext(DbContextOptions<SurveyContext> option):base(option)
        {
            
        }

        public DbSet<Page> Pages { get; set; }
        public DbSet<Banner> Banners { get; set; }

        public DbSet<Address> Addresss { get; set; }
        public DbSet<Footer> Footers { get; set; }


    }
}
