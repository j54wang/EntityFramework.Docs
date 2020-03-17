using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace ValueConversions
{
    public class Program
    {
        public static void Main()
        {
            Mapping_immutable_class_property();
            Mapping_immutable_struct_property();
            Mapping_List_property();
        }

        private static void Mapping_immutable_class_property()
        {
            ConsoleWriteLines("Sample showing value conversions for a simple immutable class...");

            CleanDatabase();

            using (var context = new SampleDbContext())
            {
                ConsoleWriteLines("Save a new entity...");

                var entity = new EntityType1 { MyProperty = new ImmutableClass(7) };
                context.Add(entity);
                context.SaveChanges();
                
                ConsoleWriteLines("Change the property value and save again...");

                // This will be detected and EF will update the database on SaveChanges
                entity.MyProperty = new ImmutableClass(77);

                context.SaveChanges();
            }
            
            using (var context = new SampleDbContext())
            {
                ConsoleWriteLines("Read the entity back...");

                var entity = context.Set<EntityType1>().Single();
                
                Debug.Assert(entity.MyProperty.Value == 77);
            }

            ConsoleWriteLines("Sample finished.");
        }

        private static void Mapping_immutable_struct_property()
        {
            ConsoleWriteLines("Sample showing value conversions for a simple immutable struct...");

            CleanDatabase();

            using (var context = new SampleDbContext())
            {
                ConsoleWriteLines("Save a new entity...");

                var entity = new EntityType2 { MyProperty = new ImmutableStruct(6) };
                context.Add(entity);
                context.SaveChanges();
                
                ConsoleWriteLines("Change the property value and save again...");

                // This will be detected and EF will update the database on SaveChanges
                entity.MyProperty = new ImmutableStruct(66);

                context.SaveChanges();
            }
            
            using (var context = new SampleDbContext())
            {
                ConsoleWriteLines("Read the entity back...");

                var entity = context.Set<EntityType2>().Single();
                
                Debug.Assert(entity.MyProperty.Value == 66);
            }

            ConsoleWriteLines("Sample finished.");
        }

        private static void Mapping_List_property()
        {
            ConsoleWriteLines("Sample showing value conversions for a List<int>...");

            CleanDatabase();

            using (var context = new SampleDbContext())
            {
                ConsoleWriteLines("Save a new entity...");

                var entity = new EntityType3 { MyProperty = new List<int> { 1, 2, 3 } };
                context.Add(entity);
                context.SaveChanges();
                
                ConsoleWriteLines("Mutate the property value and save again...");

                // This will be detected and EF will update the database on SaveChanges
                entity.MyProperty.Add(4);

                context.SaveChanges();
            }
            
            using (var context = new SampleDbContext())
            {
                ConsoleWriteLines("Read the entity back...");

                var entity = context.Set<EntityType3>().Single();
                
                Debug.Assert(entity.MyProperty.SequenceEqual(new List<int> { 1, 2, 3, 4 }));
            }

            ConsoleWriteLines("Sample finished.");
        }

        private static void ConsoleWriteLines(params string[] values)
        {
            Console.WriteLine();
            foreach (var value in values)
            {
                Console.WriteLine(value);
            }
            Console.WriteLine();
        }

        private static void CleanDatabase()
        {
            using (var context = new SampleDbContext())
            {
                ConsoleWriteLines("Deleting and re-creating database...");
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
                ConsoleWriteLines("Done. Database is clean and fresh.");
            }
        }
    }

    public class SampleDbContext : DbContext
    {
        private static readonly ILoggerFactory
            Logger = LoggerFactory.Create(x => x.AddConsole()); //.SetMinimumLevel(LogLevel.Debug));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region ConfigureImmutableClassProperty
            modelBuilder
                .Entity<EntityType1>()
                .Property(e => e.MyProperty)
                .HasConversion(
                    v => v.Value,
                    v => new ImmutableClass(v));
            #endregion

            #region ConfigureImmutableStructProperty
            modelBuilder
                .Entity<EntityType2>()
                .Property(e => e.MyProperty)
                .HasConversion(
                    v => v.Value,
                    v => new ImmutableStruct(v));
            #endregion

            #region ConfigureListProperty
            modelBuilder
                .Entity<EntityType3>()
                .Property(e => e.MyProperty)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, null),
                    v => JsonSerializer.Deserialize<List<int>>(v, null));
            #endregion

            #region ConfigureListPropertyComparer
            var valueComparer = new ValueComparer<List<int>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());
            
            modelBuilder
                .Entity<EntityType3>()
                .Property(e => e.MyProperty)
                .Metadata
                .SetValueComparer(valueComparer);
            #endregion
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseLoggerFactory(Logger)
                .UseSqlite("DataSource=test.db")
                .EnableSensitiveDataLogging();
    }

    public class EntityType1
    {
        public int Id { get; set; }
        public ImmutableClass MyProperty { get; set; }
    }

    public class EntityType2
    {
        public int Id { get; set; }
        public ImmutableStruct MyProperty { get; set; }
    }


    public class EntityType3
    {
        public int Id { get; set; }
        
        #region ListProperty
        public List<int> MyProperty { get; set; }
        #endregion
    }

    #region SimpleImmutableStruct
    public readonly struct ImmutableStruct
    {
        public ImmutableStruct(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }
    #endregion

    #region SimpleImmutableClass
    public sealed class ImmutableClass
    {
        public ImmutableClass(int value)
        {
            Value = value;
        }

        public int Value { get; }

        private bool Equals(ImmutableClass other) 
            => Value == other.Value;

        public override bool Equals(object obj) 
            => ReferenceEquals(this, obj) || obj is ImmutableClass other && Equals(other);

        public override int GetHashCode() 
            => Value;
    }
    #endregion
}
