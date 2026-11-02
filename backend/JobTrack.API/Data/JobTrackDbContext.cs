using Microsoft.EntityFrameworkCore;

namespace JobTrack.API.Data;

public sealed class JobTrackDbContext(DbContextOptions<JobTrackDbContext> options) : DbContext(options)
{
    public static readonly Guid LocalUserId = Guid.Parse("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b");

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<UserPositionPreference> UserPositionPreferences => Set<UserPositionPreference>();
    public DbSet<UserWorkPreference> UserWorkPreferences => Set<UserWorkPreference>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<ApplicationDocument> ApplicationDocuments => Set<ApplicationDocument>();
    public DbSet<ApplicationHistory> ApplicationHistory => Set<ApplicationHistory>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(user => user.Education).HasMaxLength(500);
            entity.Property(user => user.ExperienceSummary).HasMaxLength(2000);
            entity.HasData(new UserProfile
            {
                Id = LocalUserId,
                DisplayName = "Mi perfil",
                Education = "Tecnicatura en Desarrollo de Software",
                ExperienceSummary = "Sin experiencia profesional en desarrollo; programas de formación y proyectos académicos.",
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<UserSkill>(entity =>
        {
            entity.ToTable("Skills");
            entity.HasKey(skill => skill.Id);
            entity.Property(skill => skill.Name).HasMaxLength(100).IsRequired();
            entity.Property(skill => skill.Proficiency).HasMaxLength(40);
            entity.HasOne(skill => skill.UserProfile)
                .WithMany(user => user.Skills)
                .HasForeignKey(skill => skill.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPositionPreference>(entity =>
        {
            entity.ToTable("UserPositionPreferences");
            entity.HasKey(preference => preference.Id);
            entity.Property(preference => preference.Title).HasMaxLength(120).IsRequired();
            entity.Property(preference => preference.PreferenceType).HasMaxLength(30).IsRequired();
            entity.HasOne(preference => preference.UserProfile)
                .WithMany(user => user.PositionPreferences)
                .HasForeignKey(preference => preference.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                new UserPositionPreference { Id = Guid.Parse("4ba281d1-67ba-4522-a9a8-d240cbd7170a"), UserProfileId = LocalUserId, Title = ".NET Trainee / Junior", PreferenceType = "Principal" },
                new UserPositionPreference { Id = Guid.Parse("98357990-14de-4e06-9d4c-4c12e04c28f5"), UserProfileId = LocalUserId, Title = "Desarrolladora Backend Junior", PreferenceType = "Principal" },
                new UserPositionPreference { Id = Guid.Parse("0914b6a2-f70e-41c0-96d2-8b1420cf818f"), UserProfileId = LocalUserId, Title = "Desarrolladora Web Junior", PreferenceType = "Principal" },
                new UserPositionPreference { Id = Guid.Parse("db61a6a4-6e0d-4c86-a4ab-d5c3478f7cf4"), UserProfileId = LocalUserId, Title = "Programadora C#", PreferenceType = "Principal" },
                new UserPositionPreference { Id = Guid.Parse("91f940e7-a341-4d60-90b0-7ee813f2ec78"), UserProfileId = LocalUserId, Title = "Soporte Técnico IT", PreferenceType = "Alternativo" },
                new UserPositionPreference { Id = Guid.Parse("3cf6c65c-aa64-4e5e-86af-f9cd325befaa"), UserProfileId = LocalUserId, Title = "QA Tester Junior", PreferenceType = "Alternativo" },
                new UserPositionPreference { Id = Guid.Parse("81622e2e-2449-40ef-ae3d-81469f19a29b"), UserProfileId = LocalUserId, Title = "Analista funcional Junior", PreferenceType = "Alternativo" },
                new UserPositionPreference { Id = Guid.Parse("50ebc4f6-e96d-46e9-a205-8244fdc476ca"), UserProfileId = LocalUserId, Title = "Help Desk", PreferenceType = "Alternativo" });
        });

        modelBuilder.Entity<UserWorkPreference>(entity =>
        {
            entity.ToTable("UserWorkPreferences");
            entity.HasKey(preference => preference.Id);
            entity.Property(preference => preference.Preference).HasMaxLength(160).IsRequired();
            entity.HasOne(preference => preference.UserProfile)
                .WithMany(user => user.WorkPreferences)
                .HasForeignKey(preference => preference.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                new UserWorkPreference { Id = Guid.Parse("4501c04b-7926-43a8-93c0-aa8df27c82d2"), UserProfileId = LocalUserId, Preference = "Remoto" },
                new UserWorkPreference { Id = Guid.Parse("b4d8560e-3c28-4bd0-8ca7-a1ab21c71c17"), UserProfileId = LocalUserId, Preference = "Híbrido" },
                new UserWorkPreference { Id = Guid.Parse("c6cf8e5e-940a-4a81-900a-c66a482fe016"), UserProfileId = LocalUserId, Preference = "Presencial en Zona Sur / Buenos Aires" });
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Companies");
            entity.HasKey(company => company.Id);
            entity.Property(company => company.Name).HasMaxLength(160).IsRequired();
            entity.Property(company => company.NormalizedName).HasMaxLength(160).IsRequired();
            entity.Property(company => company.Details).HasMaxLength(2000);
            entity.Property(company => company.WebsiteUrl).HasMaxLength(500);
            entity.HasIndex(company => new { company.UserProfileId, company.NormalizedName }).IsUnique();
            entity.HasOne<UserProfile>()
                .WithMany()
                .HasForeignKey(company => company.UserProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JobOffer>(entity =>
        {
            entity.ToTable("Offers");
            entity.HasKey(offer => offer.Id);
            entity.Property(offer => offer.Title).HasMaxLength(160).IsRequired();
            entity.Property(offer => offer.Description).HasMaxLength(8000);
            entity.Property(offer => offer.Requirements).HasMaxLength(8000);
            entity.Property(offer => offer.Location).HasMaxLength(200);
            entity.Property(offer => offer.WorkMode).HasMaxLength(80);
            entity.Property(offer => offer.Salary).HasMaxLength(120);
            entity.Property(offer => offer.SourceName).HasMaxLength(100);
            entity.Property(offer => offer.SourceExternalId).HasMaxLength(200);
            entity.Property(offer => offer.SourceUrl).HasMaxLength(1000);
            entity.HasIndex(offer => new { offer.SourceName, offer.SourceExternalId })
                .IsUnique()
                .HasFilter("[SourceExternalId] <> ''");
            entity.HasOne(offer => offer.Company)
                .WithMany(company => company.Offers)
                .HasForeignKey(offer => offer.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.ToTable("Applications");
            entity.HasKey(application => application.Id);
            entity.Property(application => application.Position).HasMaxLength(160).IsRequired();
            entity.Property(application => application.Status).HasMaxLength(30).IsRequired();
            entity.Property(application => application.WorkMode).HasMaxLength(120);
            entity.Property(application => application.Salary).HasMaxLength(120);
            entity.Property(application => application.JobUrl).HasMaxLength(1000);
            entity.Property(application => application.JobDetails).HasMaxLength(8000);
            entity.Property(application => application.Notes).HasMaxLength(4000);
            entity.HasIndex(application => new { application.UserProfileId, application.ApplicationDate });
            entity.HasIndex(application => new { application.UserProfileId, application.JobOfferId })
                .IsUnique()
                .HasFilter("[JobOfferId] IS NOT NULL");
            entity.HasOne<UserProfile>()
                .WithMany()
                .HasForeignKey(application => application.UserProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(application => application.Company)
                .WithMany(company => company.Applications)
                .HasForeignKey(application => application.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(application => application.JobOffer)
                .WithMany()
                .HasForeignKey(application => application.JobOfferId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApplicationDocument>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.DocumentType).HasMaxLength(30).IsRequired();
            entity.Property(document => document.FileName).HasMaxLength(255).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(document => document.Content).HasColumnType("varbinary(max)").IsRequired();
            entity.HasOne(document => document.Application)
                .WithMany(application => application.Documents)
                .HasForeignKey(document => document.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationHistory>(entity =>
        {
            entity.ToTable("History");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.EventType).HasMaxLength(40).IsRequired();
            entity.Property(history => history.Description).HasMaxLength(1000).IsRequired();
            entity.HasIndex(history => new { history.ApplicationId, history.OccurredUtc });
            entity.HasOne(history => history.Application)
                .WithMany(application => application.History)
                .HasForeignKey(history => history.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Interview>(entity =>
        {
            entity.ToTable("Interviews");
            entity.HasKey(interview => interview.Id);
            entity.Property(interview => interview.InterviewType).HasMaxLength(100);
            entity.Property(interview => interview.LocationOrLink).HasMaxLength(1000);
            entity.Property(interview => interview.ContactName).HasMaxLength(160);
            entity.Property(interview => interview.Notes).HasMaxLength(2000);
            entity.HasOne(interview => interview.Application)
                .WithMany(application => application.Interviews)
                .HasForeignKey(interview => interview.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.ToTable("Reminders");
            entity.HasKey(reminder => reminder.Id);
            entity.Property(reminder => reminder.Note).HasMaxLength(1000);
            entity.HasIndex(reminder => new { reminder.ApplicationId, reminder.IsCompleted, reminder.DueDate });
            entity.HasOne(reminder => reminder.Application)
                .WithMany(application => application.Reminders)
                .HasForeignKey(reminder => reminder.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
