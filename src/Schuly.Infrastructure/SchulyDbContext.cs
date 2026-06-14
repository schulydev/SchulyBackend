using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Schuly.Domain;

namespace Schuly.Infrastructure
{
    public class SchulyDbContext : DbContext
    {
        public DbSet<Class> Classes { get; set; }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<SchoolUser> SchoolUsers { get; set; }
        public DbSet<School> Schools { get; set; }
        public DbSet<Grade> Grades { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Absence> Absences { get; set; }
        public DbSet<AgendaEntry> AgendaEntries { get; set; }
        public DbSet<StudentDocument> StudentDocuments { get; set; }
        public DbSet<SemesterReport> SemesterReports { get; set; }
        public DbSet<SemesterSubjectGrade> SemesterSubjectGrades { get; set; }
        public DbSet<Teacher> Teachers { get; set; }

        public SchulyDbContext(DbContextOptions<SchulyDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasKey(au => au.Id);
                entity.HasIndex(au => au.ExternalId).IsUnique();
                entity.HasIndex(au => au.Email).IsUnique();
                entity.Property(au => au.Email).HasMaxLength(255);
                entity.Property(au => au.DisplayName).HasMaxLength(200);

                entity.HasMany(au => au.SchoolUsers)
                    .WithOne(su => su.ApplicationUser)
                    .HasForeignKey(su => su.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<School>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Name).HasMaxLength(200).IsRequired();
                entity.Property(s => s.Description).HasMaxLength(1000);
                entity.Property(s => s.Email).HasMaxLength(255);
                entity.Property(s => s.PhoneNumber).HasMaxLength(50);
                entity.Property(s => s.Website).HasMaxLength(255);
                entity.Property(s => s.Street).HasMaxLength(200);
                entity.Property(s => s.City).HasMaxLength(100);
                entity.Property(s => s.State).HasMaxLength(100);
                entity.Property(s => s.Zip).HasMaxLength(20);
                entity.Property(s => s.Country).HasMaxLength(100);
                entity.HasIndex(s => s.Name);

                entity.HasMany(s => s.SchoolUsers)
                    .WithOne(su => su.School)
                    .HasForeignKey(su => su.SchoolId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(s => s.Classes)
                    .WithOne(c => c.School)
                    .HasForeignKey(c => c.SchoolId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SchoolUser>(entity =>
            {
                entity.HasKey(su => su.Id);
                // Email unique per school — same person at two schools is fine.
                entity.HasIndex(su => new { su.SchoolId, su.Email }).IsUnique();
                entity.Property(su => su.Email).HasMaxLength(255);
                entity.Property(su => su.PrivateEmail).HasMaxLength(255);
                entity.Property(su => su.PhoneNumber).HasMaxLength(50);
                entity.Property(su => su.FirstName).HasMaxLength(100);
                entity.Property(su => su.LastName).HasMaxLength(100);
                entity.Property(su => su.Street).HasMaxLength(200);
                entity.Property(su => su.City).HasMaxLength(100);
                entity.Property(su => su.Zip).HasMaxLength(20);
                entity.Property(su => su.Role).IsRequired();

                entity.HasOne(su => su.ApplicationUser)
                    .WithMany(au => au.SchoolUsers)
                    .HasForeignKey(su => su.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(su => su.School)
                    .WithMany(s => s.SchoolUsers)
                    .HasForeignKey(su => su.SchoolId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(su => new { su.ApplicationUserId, su.SchoolId });
                // Admin: list teachers / students of a school.
                entity.HasIndex(su => new { su.SchoolId, su.Role });
            });

            modelBuilder.Entity<Grade>(entity =>
            {
                entity.HasKey(g => g.Id);
                // Swiss grades: 1.00–6.00 with 0.25/0.5 steps; weighting 0.00–9.99.
                // Points: raw exam points (e.g. 23.5/30) before grade conversion.
                entity.Property(g => g.Score).HasPrecision(4, 2);
                entity.Property(g => g.Weighting).HasPrecision(4, 2);
                entity.Property(g => g.Points).HasPrecision(6, 2);
                entity.HasOne(g => g.SchoolUser)
                    .WithMany(su => su.Grades)
                    .HasForeignKey(g => g.SchoolUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(g => g.Exam)
                    .WithMany(e => e.Grades)
                    .HasForeignKey(g => g.ExamId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(g => new { g.SchoolUserId, g.ExamId });
                // Standalone ExamId index supports "all grades for exam X" —
                // a composite (SchoolUserId, ExamId) only helps when filtering
                // by the leading column.
                entity.HasIndex(g => g.ExamId);
            });

            modelBuilder.Entity<Exam>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.ClassAverage).HasPrecision(4, 2);
                // EF creates an FK index on ClassId automatically.
                // Composite for "exams in class X ordered by date".
                entity.HasIndex(e => new { e.ClassId, e.Date });

                entity.HasOne(e => e.Class)
                    .WithMany(c => c.Exams)
                    .HasForeignKey(e => e.ClassId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Class>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).HasMaxLength(100);
                entity.Property(c => c.Description).HasMaxLength(1000);
                entity.Property(c => c.DisplayName).HasMaxLength(300);
                entity.Property(c => c.Type).HasMaxLength(20);
                // Name unique per school — different schools can both have a "Math" class.
                entity.HasIndex(c => new { c.SchoolId, c.Name }).IsUnique();

                entity.HasMany(c => c.Students)
                      .WithMany(su => su.Classes);
                entity.HasMany(c => c.Teachers)
                      .WithMany(t => t.Classes);

                entity.HasOne(c => c.School)
                    .WithMany(s => s.Classes)
                    .HasForeignKey(c => c.SchoolId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => c.SchoolId);
                // Listing "courses I have this semester" is a hot query.
                entity.HasIndex(c => new { c.SchoolId, c.SchoolYearStart, c.SemesterHalf });

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Class_SemesterHalf",
                    "\"SemesterHalf\" IS NULL OR \"SemesterHalf\" IN (1, 2)"));
            });

            modelBuilder.Entity<Teacher>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(t => t.LastName).HasMaxLength(100).IsRequired();
                entity.Property(t => t.Code).HasMaxLength(20).IsRequired();
                entity.Property(t => t.Email).HasMaxLength(255);

                entity.HasOne(t => t.School)
                    .WithMany(s => s.Teachers)
                    .HasForeignKey(t => t.SchoolId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.ApplicationUser)
                    .WithMany(au => au.Teachers)
                    .HasForeignKey(t => t.ApplicationUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Kürzel uniquely identifies a teacher within a school.
                entity.HasIndex(t => new { t.SchoolId, t.Code }).IsUnique();
                // Resolve the current login's teacher records for per-class authz.
                entity.HasIndex(t => t.ApplicationUserId);
            });

            modelBuilder.Entity<AgendaEntry>(entity =>
            {
                entity.HasKey(ae => ae.Id);
                entity.Property(ae => ae.Title).HasMaxLength(200);
                // Composite supports "events for class X around date Y" without
                // a separate index on Date alone.
                entity.HasIndex(ae => new { ae.ClassId, ae.Date });
                // Mirror the (scope, Date) pattern for school-wide + personal
                // scopes so each is fast for "events between two dates".
                entity.HasIndex(ae => new { ae.SchoolId, ae.Date });
                entity.HasIndex(ae => new { ae.SchoolUserId, ae.Date });

                entity.HasOne(ae => ae.Class)
                    .WithMany(c => c.Agenda)
                    .HasForeignKey(ae => ae.ClassId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ae => ae.School)
                    .WithMany(s => s.Agenda)
                    .HasForeignKey(ae => ae.SchoolId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ae => ae.SchoolUser)
                    .WithMany(su => su.Agenda)
                    .HasForeignKey(ae => ae.SchoolUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.ToTable(t =>
                {
                    // Exactly one of ClassId / SchoolId / SchoolUserId must be set.
                    t.HasCheckConstraint(
                        "CK_AgendaEntry_ExactlyOneScope",
                        "(CASE WHEN \"ClassId\" IS NULL THEN 0 ELSE 1 END" +
                        " + CASE WHEN \"SchoolId\" IS NULL THEN 0 ELSE 1 END" +
                        " + CASE WHEN \"SchoolUserId\" IS NULL THEN 0 ELSE 1 END) = 1");
                    // EndDate, when set, must not precede Date.
                    t.HasCheckConstraint(
                        "CK_AgendaEntry_EndDateAfterDate",
                        "\"EndDate\" IS NULL OR \"EndDate\" >= \"Date\"");
                });
            });

            modelBuilder.Entity<Absence>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Reason).HasMaxLength(500);

                entity.HasOne(a => a.SchoolUser)
                    .WithMany(su => su.Absences)
                    .HasForeignKey(a => a.SchoolUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => new { a.SchoolUserId, a.From, a.Until, a.Type });

                // From must precede Until — otherwise period math downstream
                // (durations, overlaps) silently produces nonsense.
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Absence_FromBeforeUntil",
                    "\"From\" <= \"Until\""));
            });

            modelBuilder.Entity<StudentDocument>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Title).HasMaxLength(300).IsRequired();
                entity.Property(d => d.Comment).HasMaxLength(2000);
                entity.Property(d => d.Category).HasMaxLength(100);
                entity.Property(d => d.EnteredBy).HasMaxLength(200);
                entity.Property(d => d.FileName).HasMaxLength(300);
                entity.Property(d => d.FileUrl).HasMaxLength(2000);
                entity.Property(d => d.FollowUpAction).HasMaxLength(500);

                entity.HasOne(d => d.SchoolUser)
                    .WithMany(su => su.Documents)
                    .HasForeignKey(d => d.SchoolUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(d => new { d.SchoolUserId, d.Category });
            });

            modelBuilder.Entity<SemesterReport>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.ProgramCode).HasMaxLength(50).IsRequired();
                entity.Property(r => r.ClassName).HasMaxLength(100).IsRequired();
                entity.Property(r => r.PromotionDecision).HasMaxLength(10);
                entity.Property(r => r.GradeAverage).HasPrecision(4, 2);

                entity.HasOne(r => r.SchoolUser)
                    .WithMany(su => su.SemesterReports)
                    .HasForeignKey(r => r.SchoolUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One report per student × program × semester.
                entity.HasIndex(r => new { r.SchoolUserId, r.ProgramCode, r.SchoolYearStart, r.SemesterHalf })
                    .IsUnique();

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_SemesterReport_SemesterHalf",
                    "\"SemesterHalf\" IN (1, 2)"));
            });

            modelBuilder.Entity<SemesterSubjectGrade>(entity =>
            {
                entity.HasKey(sg => sg.Id);
                entity.Property(sg => sg.SubjectCode).HasMaxLength(50).IsRequired();
                entity.Property(sg => sg.SubjectName).HasMaxLength(200).IsRequired();
                entity.Property(sg => sg.SubjectTypeMarker).HasMaxLength(10);
                entity.Property(sg => sg.Grade).HasPrecision(4, 2);
                entity.Property(sg => sg.Marker).HasMaxLength(20);

                entity.HasOne(sg => sg.SemesterReport)
                    .WithMany(r => r.Subjects)
                    .HasForeignKey(sg => sg.SemesterReportId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One row per report × subject.
                entity.HasIndex(sg => new { sg.SemesterReportId, sg.SubjectCode }).IsUnique();
            });
        }

        public override int SaveChanges()
        {
            UpdateDateTrackingFields();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateDateTrackingFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateDateTrackingFields()
        {
            var entries = ChangeTracker.Entries<Base>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }

                if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    public class SchulyDbContextFactory : IDesignTimeDbContextFactory<SchulyDbContext>
    {
        public SchulyDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchulyDbContext>();
            optionsBuilder.UseNpgsql();
            return new SchulyDbContext(optionsBuilder.Options);
        }
    }
}
