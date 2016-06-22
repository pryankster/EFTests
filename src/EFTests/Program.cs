using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace EFTests
{
    public class Blog
    {
        public Guid id { get; set; }
        public string name { get; set; }
        public ICollection<Article> articles { get; set; }
    }

    public class Article
    {
        public Guid id { get; set; }
        [Required]
        public string title { get; set; }
        public string subtitle { get; set; }

        public Blog blog { get; set; }
    }

    public class ApplicationContext : DbContext {
        public DbSet<Blog> blogs { get; set; }
        public DbSet<Article> articles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=EFTests;Trusted_Connection=True;");
        }
    }

    public class Program
    {
        public static void QueryShouldHaveLIKE<T>(IQueryable<T> q)
        {
            TestSqlLoggerFactory.Reset();
            var results = q.ToList();
            string sql = TestSqlLoggerFactory.Sql;
            if (sql.Contains("LIKE"))
                Console.WriteLine("Sql ok");
            else
                Console.WriteLine("Sql did NOT contain LIKE: " + Environment.NewLine + sql);
        }

        public static void Main(string[] args)
        {
            using (var _context = new ApplicationContext())
            {
                var serviceProvider = _context.GetInfrastructure<IServiceProvider>();
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                loggerFactory.AddProvider(new MyLoggerProvider());

                Blog blog = _context.blogs.FirstOrDefault(e => e.name == "BlogName");
                if (blog == null)
                {
                    blog = new Blog() { id = Guid.NewGuid(), name = "BlogName" };
                    _context.Add(blog);
                    _context.SaveChanges();
                }
                if (_context.articles.FirstOrDefault(e => e.title == "Article Title") == null)
                {
                    // intentionally leave title blank
                    var article = new Article() { id = Guid.NewGuid(), blog = blog, title = "Article Title" };
                    _context.SaveChanges();
                }

                string search = "Name";
                // Expect this query to have a WHERE ... LIKE, clause: it does.
                QueryShouldHaveLIKE(_context.articles.Include(e => e.blog)
                        .Where(e => e.title.Contains(search) || e.subtitle.Contains(search)));

                // Expect this query to have a WHERE ... LIKE, clause: it does NOT.
                QueryShouldHaveLIKE(_context.articles.Include(e => e.blog)
                        .Where(e => e.blog.name.Contains(search)));

                // Expect this query to have a WHERE ... =, clause: it does NOT.
                QueryShouldHaveLIKE(_context.articles.Include(e => e.blog)
                        .Where(e => e.blog.name == search));

                // Expect this query to have a WHERE ... LIKE clause: it does NOT.
                QueryShouldHaveLIKE(_context.articles.Include(e => e.blog)
                        .Where(e => e.title.Contains(search) || e.subtitle.Contains(search) || e.blog.name.Contains(search)));
            }
        }

        public class MyLoggerProvider : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName)
            {
                ILoggerFactory factory = new TestSqlLoggerFactory();
                return factory.CreateLogger(categoryName);
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }

        public class TestSqlLoggerFactory : ILoggerFactory
        {
            private static SqlLogger _logger;
            private static readonly string EOL = Environment.NewLine;

            public ILogger CreateLogger(string name) => Logger;

            private static SqlLogger Logger => LazyInitializer.EnsureInitialized(ref _logger);

            public void AddProvider(ILoggerProvider provider)
            {
                throw new NotImplementedException();
            }

            public CancellationToken CancelQuery()
            {
                Logger.SqlLoggerData._cancellationTokenSource = new CancellationTokenSource();

                return Logger.SqlLoggerData._cancellationTokenSource.Token;
            }

            public static void Reset() => Logger.ResetLoggerData();

            // public static void CaptureOutput(ITestOutputHelper testOutputHelper) => Logger.SqlLoggerData._testOutputHelper = testOutputHelper;

            public void Dispose()
            {
            }

            public static string Log => Logger.SqlLoggerData.LogText;

            public static string Sql
                => string.Join(EOL + EOL, Logger.SqlLoggerData._sqlStatements);

            public static IReadOnlyList<string> SqlStatements => Logger.SqlLoggerData._sqlStatements;

            public static IReadOnlyList<DbCommandLogData> CommandLogData => Logger.SqlLoggerData._logData;

#if NET451
        [Serializable]
#endif
            private class SqlLoggerData
            {
                public string LogText => _log.ToString();

                // ReSharper disable InconsistentNaming
#if NET451
            [NonSerialized]
#endif
                public readonly IndentedStringBuilder _log = new IndentedStringBuilder();
                public readonly List<string> _sqlStatements = new List<string>();
#if NET451
            [NonSerialized]
#endif
                public readonly List<DbCommandLogData> _logData = new List<DbCommandLogData>();
#if NET451
            [NonSerialized]
#endif
                // public ITestOutputHelper _testOutputHelper;
#if NET451
            [NonSerialized]
#endif
                public CancellationTokenSource _cancellationTokenSource;
                // ReSharper restore InconsistentNaming
            }

            // ReSharper disable once ClassNeverInstantiated.Local
            private class SqlLogger : ILogger
            {
                private readonly static AsyncLocal<SqlLoggerData> _loggerData = new AsyncLocal<SqlLoggerData>();

                // ReSharper disable once MemberCanBeMadeStatic.Local
                public SqlLoggerData SqlLoggerData
                {
                    get
                    {
                        var loggerData = _loggerData.Value;
                        return loggerData ?? CreateLoggerData();
                    }
                }

                private static SqlLoggerData CreateLoggerData()
                {
                    var loggerData = new SqlLoggerData();
                    _loggerData.Value = loggerData;
                    return loggerData;
                }

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception exception,
                    Func<TState, Exception, string> formatter)
                {
                    var format = formatter(state, exception)?.Trim();

                    if (format != null)
                    {
                        var sqlLoggerData = SqlLoggerData;

                        if (sqlLoggerData._cancellationTokenSource != null)
                        {
                            sqlLoggerData._cancellationTokenSource.Cancel();
                            sqlLoggerData._cancellationTokenSource = null;
                        }

                        var commandLogData = state as DbCommandLogData;

                        if (commandLogData != null)
                        {
                            var parameters = "";

                            if (commandLogData.Parameters.Any())
                            {
                                parameters
                                    = string.Join(
                                        EOL,
                                        commandLogData.Parameters
                                            .Select(p => $"{p.Key}: " + p.Value.ToString()))
                                        + EOL + EOL;
                            }

                            sqlLoggerData._sqlStatements.Add(parameters + commandLogData.CommandText);

                            sqlLoggerData._logData.Add(commandLogData);
                        }

                        else
                        {
                            sqlLoggerData._log.AppendLine(format);
                        }

                        File.AppendAllText(@"c:\temp\sql.txt", format + Environment.NewLine);
                        // sqlLoggerData._testOutputHelper?.WriteLine(format + Environment.NewLine);
                    }
                }


                public bool IsEnabled(LogLevel logLevel) => true;

                public IDisposable BeginScope<TState>(TState state) => SqlLoggerData._log.Indent();

                // ReSharper disable once MemberCanBeMadeStatic.Local
                public void ResetLoggerData() =>
                        _loggerData.Value = null;
            }
        }
    }
}
