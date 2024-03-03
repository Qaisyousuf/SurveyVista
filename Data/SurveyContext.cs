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

        public DbSet<SocialMedia> SocialMedia { get; set; }


        public DbSet<FooterSocialMedia> FooterSocialMedias { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FooterSocialMedia>()
            .HasKey(fsm => new { fsm.FooterId, fsm.SocialId });

            modelBuilder.Entity<FooterSocialMedia>()
                .HasOne(fsm => fsm.Footer)
                .WithMany(f => f.FooterSocialMedias)
                .HasForeignKey(fsm => fsm.FooterId);

            modelBuilder.Entity<FooterSocialMedia>()
                .HasOne(fsm => fsm.SocialMedia)
                .WithMany(s => s.FooterSocialMedias)
                .HasForeignKey(fsm => fsm.SocialId);




            base.OnModelCreating(modelBuilder);
        }


    }
}
