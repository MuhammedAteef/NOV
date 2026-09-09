using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SystemManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndProductPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add PurchasePrice to Products if not present
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Products') AND name = 'PurchasePrice')
                BEGIN
                    ALTER TABLE Products ADD PurchasePrice decimal(10, 2) NOT NULL DEFAULT 0;
                END
            ");

            // 2. Drop Notes from Payments if present (per user schema cleanup)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Payments') AND name = 'Notes')
                BEGIN
                    ALTER TABLE Payments DROP COLUMN Notes;
                END
            ");

            // 3. Create AspNetRoles if not present
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'AspNetRoles') IS NULL
                BEGIN
                    CREATE TABLE [AspNetRoles] (
                        [Id] nvarchar(450) NOT NULL,
                        [Name] nvarchar(256) NULL,
                        [NormalizedName] nvarchar(256) NULL,
                        [ConcurrencyStamp] nvarchar(max) NULL,
                        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
                END
            ");

            // 4. Create AspNetUsers if not present
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'AspNetUsers') IS NULL
                BEGIN
                    CREATE TABLE [AspNetUsers] (
                        [Id] nvarchar(450) NOT NULL,
                        [UserName] nvarchar(256) NULL,
                        [NormalizedUserName] nvarchar(256) NULL,
                        [Email] nvarchar(256) NULL,
                        [NormalizedEmail] nvarchar(256) NULL,
                        [EmailConfirmed] bit NOT NULL,
                        [PasswordHash] nvarchar(max) NULL,
                        [SecurityStamp] nvarchar(max) NULL,
                        [ConcurrencyStamp] nvarchar(max) NULL,
                        [PhoneNumber] nvarchar(max) NULL,
                        [PhoneNumberConfirmed] bit NOT NULL,
                        [TwoFactorEnabled] bit NOT NULL,
                        [LockoutEnd] datetimeoffset NULL,
                        [LockoutEnabled] bit NOT NULL,
                        [AccessFailedCount] int NOT NULL,
                        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
                    CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
                END
            ");

            // 5. Create AspNetRoleClaims if not present
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'AspNetRoleClaims') IS NULL
                BEGIN
                    CREATE TABLE [AspNetRoleClaims] (
                        [Id] int NOT NULL IDENTITY,
                        [RoleId] nvarchar(450) NOT NULL,
                        [ClaimType] nvarchar(max) NULL,
                        [ClaimValue] nvarchar(max) NULL,
                        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
                END
            ");

            // 6. Create AspNetUserClaims if not present
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'AspNetUserClaims') IS NULL
                BEGIN
                    CREATE TABLE [AspNetUserClaims] (
                        [Id] int NOT NULL IDENTITY,
                        [UserId] nvarchar(450) NOT NULL,
                        [ClaimType] nvarchar(max) NULL,
                        [ClaimValue] nvarchar(max) NULL,
                        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
                END
            ");

            // 7. Create AspNetUserLogins if not present
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'AspNetUserLogins') IS NULL
                BEGIN
                    CREATE TABLE [AspNetUserLogins] (
                        [LoginProvider] nvarchar(450) NOT NULL,
                        [ProviderKey] nvarchar(450) NOT NULL,
                        [ProviderDisplayName] nvarchar(max) NULL,
                        [UserId] nvarchar(450) NOT NULL,
                        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
                        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
                END
            ");

            // 8. Create AspNetUserRoles if not present
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'AspNetUserRoles') IS NULL
                BEGIN
                    CREATE TABLE [AspNetUserRoles] (
                        [UserId] nvarchar(450) NOT NULL,
                        [RoleId] nvarchar(450) NOT NULL,
                        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
                        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
                END
            ");

            // 9. Create AspNetUserTokens if not present
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'AspNetUserTokens') IS NULL
                BEGIN
                    CREATE TABLE [AspNetUserTokens] (
                        [UserId] nvarchar(450) NOT NULL,
                        [LoginProvider] nvarchar(450) NOT NULL,
                        [Name] nvarchar(450) NOT NULL,
                        [Value] nvarchar(max) NULL,
                        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
                        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID(N'AspNetUserTokens') IS NOT NULL DROP TABLE AspNetUserTokens;");
            migrationBuilder.Sql("IF OBJECT_ID(N'AspNetUserRoles') IS NOT NULL DROP TABLE AspNetUserRoles;");
            migrationBuilder.Sql("IF OBJECT_ID(N'AspNetUserLogins') IS NOT NULL DROP TABLE AspNetUserLogins;");
            migrationBuilder.Sql("IF OBJECT_ID(N'AspNetUserClaims') IS NOT NULL DROP TABLE AspNetUserClaims;");
            migrationBuilder.Sql("IF OBJECT_ID(N'AspNetRoleClaims') IS NOT NULL DROP TABLE AspNetRoleClaims;");
            migrationBuilder.Sql("IF OBJECT_ID(N'AspNetRoles') IS NOT NULL DROP TABLE AspNetRoles;");
            migrationBuilder.Sql("IF OBJECT_ID(N'AspNetUsers') IS NOT NULL DROP TABLE AspNetUsers;");
            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Products') AND name = 'PurchasePrice') ALTER TABLE Products DROP COLUMN PurchasePrice;");
        }
    }
}
