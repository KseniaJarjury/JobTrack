using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JobTrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Education = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExperienceSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_Users_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Proficiency = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Users_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPositionPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PreferenceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPositionPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPositionPreferences_Users_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Preference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWorkPreferences_Users_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Requirements = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkMode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Salary = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublishedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Offers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobOfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Position = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    WorkMode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Salary = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    JobUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    JobDetails = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Applications_Offers_JobOfferId",
                        column: x => x.JobOfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Applications_Users_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                    table.ForeignKey(
                        name: "FK_History_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Interviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InterviewType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocationOrLink = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interviews_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reminders_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedUtc", "DisplayName", "Education", "ExperienceSummary" },
                values: new object[] { new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Mi perfil", "Tecnicatura en Desarrollo de Software", "Sin experiencia profesional en desarrollo; programas de formación y proyectos académicos." });

            migrationBuilder.InsertData(
                table: "UserPositionPreferences",
                columns: new[] { "Id", "PreferenceType", "Title", "UserProfileId" },
                values: new object[,]
                {
                    { new Guid("0914b6a2-f70e-41c0-96d2-8b1420cf818f"), "Principal", "Desarrolladora Web Junior", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("3cf6c65c-aa64-4e5e-86af-f9cd325befaa"), "Alternativo", "QA Tester Junior", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("4ba281d1-67ba-4522-a9a8-d240cbd7170a"), "Principal", ".NET Trainee / Junior", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("50ebc4f6-e96d-46e9-a205-8244fdc476ca"), "Alternativo", "Help Desk", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("81622e2e-2449-40ef-ae3d-81469f19a29b"), "Alternativo", "Analista funcional Junior", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("91f940e7-a341-4d60-90b0-7ee813f2ec78"), "Alternativo", "Soporte Técnico IT", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("98357990-14de-4e06-9d4c-4c12e04c28f5"), "Principal", "Desarrolladora Backend Junior", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("db61a6a4-6e0d-4c86-a4ab-d5c3478f7cf4"), "Principal", "Programadora C#", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") }
                });

            migrationBuilder.InsertData(
                table: "UserWorkPreferences",
                columns: new[] { "Id", "Preference", "UserProfileId" },
                values: new object[,]
                {
                    { new Guid("4501c04b-7926-43a8-93c0-aa8df27c82d2"), "Remoto", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("b4d8560e-3c28-4bd0-8ca7-a1ab21c71c17"), "Híbrido", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") },
                    { new Guid("c6cf8e5e-940a-4a81-900a-c66a482fe016"), "Presencial en Zona Sur / Buenos Aires", new Guid("a85dba15-fb20-4bdb-92b8-f8a0a13c4e1b") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CompanyId",
                table: "Applications",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobOfferId",
                table: "Applications",
                column: "JobOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserProfileId_ApplicationDate",
                table: "Applications",
                columns: new[] { "UserProfileId", "ApplicationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_UserProfileId_JobOfferId",
                table: "Applications",
                columns: new[] { "UserProfileId", "JobOfferId" },
                unique: true,
                filter: "[JobOfferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_UserProfileId_NormalizedName",
                table: "Companies",
                columns: new[] { "UserProfileId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ApplicationId",
                table: "Documents",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_History_ApplicationId_OccurredUtc",
                table: "History",
                columns: new[] { "ApplicationId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_ApplicationId",
                table: "Interviews",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_CompanyId",
                table: "Offers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_SourceName_SourceExternalId",
                table: "Offers",
                columns: new[] { "SourceName", "SourceExternalId" },
                unique: true,
                filter: "[SourceExternalId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_ApplicationId_IsCompleted_DueDate",
                table: "Reminders",
                columns: new[] { "ApplicationId", "IsCompleted", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_UserProfileId",
                table: "Skills",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPositionPreferences_UserProfileId",
                table: "UserPositionPreferences",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkPreferences_UserProfileId",
                table: "UserWorkPreferences",
                column: "UserProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "History");

            migrationBuilder.DropTable(
                name: "Interviews");

            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "UserPositionPreferences");

            migrationBuilder.DropTable(
                name: "UserWorkPreferences");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "Offers");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
