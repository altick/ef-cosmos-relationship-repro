using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EFCosmosRelationshipRepro
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Initializing context");

            // Querying entity with owned type that has a foreign key to another entity within the same context will return expected results.
            AssertSameContext();
            // Querying entity with owned type that has a foreign key to another entity within two different contexts
            // will return only the first result correct, the rest of the results have the foreign entities set to null.
            AssertSeparatedContexts();
        }

        static void AssertSameContext()
        {
            Console.WriteLine("Asserting same context scenario");
            using (var context = CreateContext())
            {
                PrepareDatabase(context);
                Query(context);
            }
        }

        static void AssertSeparatedContexts()
        {
            Console.WriteLine("Asserting separated contexts scenario");
            using (var context = CreateContext())
            {
                PrepareDatabase(context);
            }
            using (var context = CreateContext())
            {
                Query(context);
            }
        }

        static void PrepareDatabase(CosmosContext context)
        {
            Console.WriteLine("Creating database");
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            Console.WriteLine("Seeding database");

            context.Users.AddRange(Seeds.Users);
            context.Activities.AddRange(Seeds.Activities);
            context.SaveChanges();
        }

        static void Query(CosmosContext context)
        {
            Console.WriteLine("Querying data");
            var activities = context.Activities
                .Include(a => a.Members)
                .ThenInclude(m => m.User)
                .ToArray();

            Debug.Assert(activities.All(a => a.Members.All(m => m.User != null)), "User instance in Member is null");
        }

        static CosmosContext CreateContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<CosmosContext>();
            optionsBuilder.UseCosmos("https://localhost:8081", "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==", "Test");
            var context = new CosmosContext(optionsBuilder.Options);
            return context;
        }
    }

    class CosmosContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        public DbSet<Activity> Activities { get; set; }

        public CosmosContext(DbContextOptions<CosmosContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<User>(builder =>
            {
                builder.Metadata.Cosmos().ContainerName = "Users";
            });

            modelBuilder.Entity<Activity>(builder =>
            {
                builder.Metadata.Cosmos().ContainerName = "Activities";
                builder.OwnsMany(a => a.Members)
                    .HasOne(m => m.User)
                    .WithMany()
                    .HasForeignKey(m => m.UserId);
            });
        }
    }

    class Activity
    {
        public Guid Id { get; set; }

        public string Title { get; set; }

        public IList<MemberShip> Members { get; set; } = new List<MemberShip>();
    }

    class MemberShip
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string Role { get; set; }

        public User User { get; set; }
    }

    class User
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
    }

    static class Seeds {

        public static User[] Users = new[]
        {
            new User { Id = Guid.NewGuid(), Name = "John" },
            new User { Id = Guid.NewGuid(), Name = "Mark" },
        };

        public static Activity[] Activities = new[]
        {
            new Activity { Id = Guid.NewGuid(), Title = "Activity 3", Members = {
                    new MemberShip { Id = Guid.NewGuid(), Role = "Owner", UserId = Users[0].Id },
                    new MemberShip { Id = Guid.NewGuid(), Role = "Member", UserId = Users[1].Id },
            } },
            new Activity { Id = Guid.NewGuid(), Title = "Activity 1", Members = {
                    new MemberShip { Id = Guid.NewGuid(), Role = "Owner", UserId = Users[0].Id },
            } },
            new Activity { Id = Guid.NewGuid(), Title = "Activity 2", Members = {
                    new MemberShip { Id = Guid.NewGuid(), Role = "Owner", UserId = Users[0].Id },
            } },
            
        };

    }
}
