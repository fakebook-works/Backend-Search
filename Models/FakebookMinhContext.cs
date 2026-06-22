using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Backend_Search_Fakebook.Models;

public partial class FakebookMinhContext : DbContext
{
    public FakebookMinhContext()
    {
    }

    public FakebookMinhContext(DbContextOptions<FakebookMinhContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Object> Objects { get; set; }

    public virtual DbSet<Token> Tokens { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Object>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("objects_pkey");

            entity.ToTable("objects");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.PrivacyLevel)
                .HasDefaultValue(2)
                .HasColumnName("privacy_level");
            entity.Property(e => e.SortKey)
                .HasDefaultValue(0)
                .HasColumnName("sort_key");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
        });

        modelBuilder.Entity<Token>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tokens_pkey");

            entity.ToTable("tokens");

            entity.HasIndex(e => e.TokenText, "tokens_token_text_key").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.TokenText)
                .HasMaxLength(255)
                .HasColumnName("token_text");

            entity.HasMany(d => d.Objects).WithMany(p => p.Tokens)
                .UsingEntity<Dictionary<string, object>>(
                    "TokenObject",
                    r => r.HasOne<Object>().WithMany()
                        .HasForeignKey("ObjectId")
                        .HasConstraintName("token_object_object_id_fkey"),
                    l => l.HasOne<Token>().WithMany()
                        .HasForeignKey("TokenId")
                        .HasConstraintName("token_object_token_id_fkey"),
                    j =>
                    {
                        j.HasKey("TokenId", "ObjectId").HasName("token_object_pkey");
                        j.ToTable("token_object");
                        j.HasIndex(new[] { "ObjectId" }, "idx_token_object_obj_id");
                        j.IndexerProperty<long>("TokenId").HasColumnName("token_id");
                        j.IndexerProperty<long>("ObjectId").HasColumnName("object_id");
                    });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
