using System.Data;
using System.Linq.Expressions;
using AudioSnapServer.Models.ResponseStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace AudioSnapServer.Data;

public class AudioSnapDbContext : DbContext
{
    public AudioSnapDbContext(DbContextOptions options) : base (options) { }

    public long GetAbsDiff(long a, long b) => throw new NotSupportedException();
    
    public uint GetBitDiff(uint fpHash, uint expectedHash) => throw new NotSupportedException();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder) 
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .HasDbFunction(typeof(AudioSnapDbContext).GetMethod(nameof(GetAbsDiff), new[] { typeof(long), typeof(long) }))
            .HasTranslation(
                args => new SqlFunctionExpression(
                    "ABS",
                    new SqlExpression[]
                    {
                        new SqlBinaryExpression(
                            ExpressionType.Subtract,
                            args.First(),
                            args.Skip(1).First(),
                            args.First().Type,
                            args.First().TypeMapping)
                    },
                    nullable: true,
                    argumentsPropagateNullability: new[] { true, true },
                    type: args.First().Type,
                    typeMapping: args.First().TypeMapping));
        
        modelBuilder.HasDbFunction(
                typeof(AudioSnapDbContext).GetMethod(nameof(GetBitDiff),
                    new[] { typeof(uint), typeof(uint) }))
            .HasTranslation(
                args => new SqlFunctionExpression(
                    "BIT_COUNT",
                    new SqlExpression[]
                    {
                        new SqlBinaryExpression(
                            ExpressionType.Or,
                            new SqlBinaryExpression(
                                ExpressionType.And,
                                new SqlUnaryExpression(
                                    ExpressionType.Not,
                                    args.First(),
                                    args.First().Type,
                                    args.First().TypeMapping
                                    ),
                                args.Skip(1).First(),
                                args.First().Type,
                                args.First().TypeMapping
                                ),
                            new SqlBinaryExpression(
                                ExpressionType.And,
                                args.First(),
                                new SqlUnaryExpression(
                                    ExpressionType.Not,
                                    args.Skip(1).First(),
                                    args.Skip(1).First().Type,
                                    args.Skip(1).First().TypeMapping),
                                args.First().Type,
                                args.First().TypeMapping
                                ),
                            args.First().Type,
                            args.First().TypeMapping)
                    },
                    nullable:true,
                    argumentsPropagateNullability: new[] { true, true },
                    type: args.First().Type,
                    typeMapping: args.First().TypeMapping
                    ));
        
        
        
    }

    public DbSet<AcoustIDStorage> AcoustIDs { get; set; }
    public DbSet<RecordingStorage> Recordings { get; set; }
    public DbSet<ReleaseStorage> Releases { get; set; }
}