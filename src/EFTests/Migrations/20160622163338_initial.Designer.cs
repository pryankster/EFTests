using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using EFTests;

namespace EFTests.Migrations
{
    [DbContext(typeof(ApplicationContext))]
    [Migration("20160622163338_initial")]
    partial class initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.0.0-rc2-20901")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("EFTests.Article", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid?>("blogid");

                    b.Property<string>("subtitle");

                    b.Property<string>("title")
                        .IsRequired();

                    b.HasKey("id");

                    b.HasIndex("blogid");

                    b.ToTable("articles");
                });

            modelBuilder.Entity("EFTests.Blog", b =>
                {
                    b.Property<Guid>("id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("name");

                    b.HasKey("id");

                    b.ToTable("blogs");
                });

            modelBuilder.Entity("EFTests.Article", b =>
                {
                    b.HasOne("EFTests.Blog")
                        .WithMany()
                        .HasForeignKey("blogid");
                });
        }
    }
}
