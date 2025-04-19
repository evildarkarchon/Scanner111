// Scanner111.Infrastructure/Persistence/Migrations/20250418InitialCreate.cs
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Scanner111.Infrastructure.Persistence;

#nullable disable

namespace Scanner111.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrashLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CrashTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GameVersion = table.Column<string>(type: "TEXT", nullable: false),
                    CrashGenVersion = table.Column<string>(type: "TEXT", nullable: false),
                    MainError = table.Column<string>(type: "TEXT", nullable: false),
                    IsAnalyzed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSolved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrashLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExecutableName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DocumentsPath = table.Column<string>(type: "TEXT", nullable: false),
                    InstallPath = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModIssues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    PluginName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    IssueType = table.Column<int>(type: "INTEGER", nullable: false),
                    Solution = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    LoadOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOfficial = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMaster = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasIssues = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "CrashLogCallStacks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CrashLogId = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Entry = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrashLogCallStacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrashLogCallStacks_CrashLogs_CrashLogId",
                        column: x => x.CrashLogId,
                        principalTable: "CrashLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrashLogIssues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CrashLogId = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrashLogIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrashLogIssues_CrashLogs_CrashLogId",
                        column: x => x.CrashLogId,
                        principalTable: "CrashLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrashLogPlugins",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CrashLogId = table.Column<string>(type: "TEXT", nullable: false),
                    PluginName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    LoadOrderId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrashLogPlugins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrashLogPlugins_CrashLogs_CrashLogId",
                        column: x => x.CrashLogId,
                        principalTable: "CrashLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrashLogCallStacks_CrashLogId",
                table: "CrashLogCallStacks",
                column: "CrashLogId");

            migrationBuilder.CreateIndex(
                name: "IX_CrashLogIssues_CrashLogId",
                table: "CrashLogIssues",
                column: "CrashLogId");

            migrationBuilder.CreateIndex(
                name: "IX_CrashLogPlugins_CrashLogId",
                table: "CrashLogPlugins",
                column: "CrashLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrashLogCallStacks");

            migrationBuilder.DropTable(
                name: "CrashLogIssues");

            migrationBuilder.DropTable(
                name: "CrashLogPlugins");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "ModIssues");

            migrationBuilder.DropTable(
                name: "Plugins");

            migrationBuilder.DropTable(
                name: "CrashLogs");
        }
    }
}

// Scanner111.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs
// <auto-generated />

#nullable disable

namespace Scanner111.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.4");

            modelBuilder.Entity("Scanner111.Core.Models.CrashLog", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CrashTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("CrashGenVersion")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("TEXT");

                    b.Property<string>("GameId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("GameVersion")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsAnalyzed")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsSolved")
                        .HasColumnType("INTEGER");

                    b.Property<string>("MainError")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("CrashLogs");
                });

            modelBuilder.Entity("Scanner111.Core.Models.CrashLogCallStack", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("CrashLogId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Entry")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Order")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("CrashLogId");

                    b.ToTable("CrashLogCallStacks");
                });

            modelBuilder.Entity("Scanner111.Core.Models.CrashLogIssue", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("CrashLogId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("CrashLogId");

                    b.ToTable("CrashLogIssues");
                });

            modelBuilder.Entity("Scanner111.Core.Models.CrashLogPlugin", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("CrashLogId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("LoadOrderId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("PluginName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("CrashLogId");

                    b.ToTable("CrashLogPlugins");
                });

            modelBuilder.Entity("Scanner111.Core.Models.Game", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("DocumentsPath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ExecutableName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("InstallPath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("Version")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Games");
                });

            modelBuilder.Entity("Scanner111.Core.Models.ModIssue", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("TEXT");

                    b.Property<int>("IssueType")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PluginName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<int>("Severity")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Solution")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("ModIssues");
                });

            modelBuilder.Entity("Scanner111.Core.Models.Plugin", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("TEXT");

                    b.Property<bool>("HasIssues")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsMaster")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsOfficial")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LoadOrderId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.HasKey("Name");

                    b.ToTable("Plugins");
                });

            modelBuilder.Entity("Scanner111.Core.Models.CrashLogCallStack", b =>
                {
                    b.HasOne("Scanner111.Core.Models.CrashLog", "CrashLog")
                        .WithMany("CallStackEntries")
                        .HasForeignKey("CrashLogId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CrashLog");
                });

            modelBuilder.Entity("Scanner111.Core.Models.CrashLogIssue", b =>
                {
                    b.HasOne("Scanner111.Core.Models.CrashLog", "CrashLog")
                        .WithMany("DetectedIssues")
                        .HasForeignKey("CrashLogId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CrashLog");
                });

            modelBuilder.Entity("Scanner111.Core.Models.CrashLogPlugin", b =>
                {
                    b.HasOne("Scanner111.Core.Models.CrashLog", "CrashLog")
                        .WithMany("Plugins")
                        .HasForeignKey("CrashLogId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CrashLog");
                });

            modelBuilder.Entity("Scanner111.Core.Models.CrashLog", b =>
                {
                    b.Navigation("CallStackEntries");

                    b.Navigation("DetectedIssues");

                    b.Navigation("Plugins");
                });
#pragma warning restore 612, 618
        }
    }
}