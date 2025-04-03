using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace CICD.EFModels.EFModels;

public partial class EFDataModel : DbContext
{
    public EFDataModel()
    {
    }

    public EFDataModel(DbContextOptions<EFDataModel> options)
        : base(options)
    {
    }

    // Uncomment the following line to when doing 'add-migration' and 'update-database' commands
    //    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //    => optionsBuilder.UseSqlServer("Data Source=(local);Initial Catalog=CRM;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True;");

    public virtual DbSet<FileStorage> FileStorages { get; set; }


    public virtual DbSet<Setting> Settings { get; set; }


    public virtual DbSet<Tenant> Tenants { get; set; }


    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {   
        modelBuilder.Entity<FileStorage>(entity =>
        {
            entity.HasKey(e => e.FileId);

            entity.ToTable("FileStorage");

            entity.HasIndex(e => e.UserId, "IX_FileStorage_UserId");

            entity.Property(e => e.FileId).ValueGeneratedNever();
            entity.Property(e => e.DeletedAt).HasColumnType("datetime");
            entity.Property(e => e.Extension).HasMaxLength(15);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.LastModified).HasColumnType("datetime");
            entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
            entity.Property(e => e.SourceFileId).HasMaxLength(100);
            entity.Property(e => e.UploadDate).HasColumnType("datetime");
            entity.Property(e => e.UploadedBy).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.FileStorages)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("IX_FileStorage_UserId");
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.SettingId).HasName("PK_Settings_1");

            entity.Property(e => e.LastModified).HasColumnType("datetime");
            entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
            entity.Property(e => e.SettingName).HasMaxLength(100);
            entity.Property(e => e.SettingType).HasMaxLength(100);
        });


        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.Property(e => e.TenantId).ValueGeneratedNever();
            entity.Property(e => e.Added).HasColumnType("datetime");
            entity.Property(e => e.AddedBy).HasMaxLength(100);
            entity.Property(e => e.LastModified).HasColumnType("datetime");
            entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.TenantCode).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.TenantId, "IX_Users_TenantId");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Added).HasColumnType("datetime");
            entity.Property(e => e.AddedBy).HasMaxLength(100);
            entity.Property(e => e.DeletedAt).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.EmployeeId).HasMaxLength(50);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastLockoutDate).HasColumnType("datetime");
            entity.Property(e => e.LastLogin).HasColumnType("datetime");
            entity.Property(e => e.LastLoginSource).HasMaxLength(50);
            entity.Property(e => e.LastModified).HasColumnType("datetime");
            entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Source).HasMaxLength(100);
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.Property(e => e.Username).HasMaxLength(100);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Users)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("IX_Users_TenantId");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
