using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Model;

namespace Data
{
    public class SurveyContext: IdentityDbContext<ApplicationUser, IdentityRole, string>
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

        public DbSet<Questionnaire> Questionnaires { get; set; }

        public DbSet<Question> Questions { get; set; }
        public DbSet<Answer> Answers { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; }

        public DbSet<Response> Responses { get; set; }
        public DbSet<ResponseDetail> ResponseDetails { get; set; }
        public DbSet<ResponseAnswer> ResponseAnswers { get; set; }

        public DbSet<SentNewsletterEamil> SentNewsletterEamils { get; set; }


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


            modelBuilder.Entity<Questionnaire>()
                 .HasKey(q => q.Id);

            modelBuilder.Entity<Questionnaire>()
                .HasMany(q => q.Questions)
                .WithOne(qn => qn.Questionnaire)
                .HasForeignKey(qn => qn.QuestionnaireId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Question>()
          .HasOne(q => q.Questionnaire)
          .WithMany(qn => qn.Questions)
          .HasForeignKey(q => q.QuestionnaireId)
          .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Answer>()
                .HasKey(a => a.Id);


            // Questionnaire to Response relationship
            modelBuilder.Entity<ResponseDetail>()
            .HasOne(rd => rd.Response)
            .WithMany(r => r.ResponseDetails)
            .HasForeignKey(rd => rd.ResponseId)
            .OnDelete(DeleteBehavior.Cascade);  // This is safe if only Responses are being deleted leading to ResponseDetails

            modelBuilder.Entity<ResponseDetail>()
                .HasOne(rd => rd.Question)
                .WithMany()
                .HasForeignKey(rd => rd.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);


            base.OnModelCreating(modelBuilder);
        }


    }
}
