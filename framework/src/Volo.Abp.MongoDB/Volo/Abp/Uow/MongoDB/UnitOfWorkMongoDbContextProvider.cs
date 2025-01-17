﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;
using Volo.Abp.Threading;

namespace Volo.Abp.Uow.MongoDB
{
    public class UnitOfWorkMongoDbContextProvider<TMongoDbContext> : IMongoDbContextProvider<TMongoDbContext>
        where TMongoDbContext : IAbpMongoDbContext
    {
        public ILogger<UnitOfWorkMongoDbContextProvider<TMongoDbContext>> Logger { get; set; }

        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IConnectionStringResolver _connectionStringResolver;
        private readonly ICancellationTokenProvider _cancellationTokenProvider;

        public UnitOfWorkMongoDbContextProvider(
            IUnitOfWorkManager unitOfWorkManager,
            IConnectionStringResolver connectionStringResolver,
            ICancellationTokenProvider cancellationTokenProvider)
        {
            _unitOfWorkManager = unitOfWorkManager;
            _connectionStringResolver = connectionStringResolver;
            _cancellationTokenProvider = cancellationTokenProvider;

            Logger = NullLogger<UnitOfWorkMongoDbContextProvider<TMongoDbContext>>.Instance;
        }

        [Obsolete("Use CreateDbContextAsync")]
        public TMongoDbContext GetDbContext()
        {
            Logger.LogWarning(
                "UnitOfWorkDbContextProvider.GetDbContext is deprecated. Use GetDbContextAsync instead! " +
                "You are probably using LINQ (LINQ extensions) directly on a repository. In this case, use repository.GetQueryableAsync() method " +
                "to obtain an IQueryable<T> instance and use LINQ (LINQ extensions) on this object. "
            );
            Logger.LogWarning(Environment.StackTrace.Truncate(2048));

            var unitOfWork = _unitOfWorkManager.Current;
            if (unitOfWork == null)
            {
                throw new AbpException(
                    $"A {nameof(IMongoDatabase)} instance can only be created inside a unit of work!");
            }

            var connectionString = _connectionStringResolver.Resolve<TMongoDbContext>();
            var dbContextKey = $"{typeof(TMongoDbContext).FullName}_{connectionString}";

            var mongoUrl = new MongoUrl(connectionString);
            var databaseName = mongoUrl.DatabaseName;
            if (databaseName.IsNullOrWhiteSpace())
            {
                databaseName = ConnectionStringNameAttribute.GetConnStringName<TMongoDbContext>();
            }

            //TODO: Create only single MongoDbClient per connection string in an application (extract MongoClientCache for example).
            var databaseApi = unitOfWork.GetOrAddDatabaseApi(
                dbContextKey,
                () => new MongoDbDatabaseApi<TMongoDbContext>(CreateDbContext(unitOfWork, mongoUrl, databaseName)));

            return ((MongoDbDatabaseApi<TMongoDbContext>) databaseApi).DbContext;
        }

        public async Task<TMongoDbContext> GetDbContextAsync(CancellationToken cancellationToken = default)
        {
            var unitOfWork = _unitOfWorkManager.Current;
            if (unitOfWork == null)
            {
                throw new AbpException(
                    $"A {nameof(IMongoDatabase)} instance can only be created inside a unit of work!");
            }

            var connectionString = await _connectionStringResolver.ResolveAsync<TMongoDbContext>();
            var dbContextKey = $"{typeof(TMongoDbContext).FullName}_{connectionString}";

            var mongoUrl = new MongoUrl(connectionString);
            var databaseName = mongoUrl.DatabaseName;
            if (databaseName.IsNullOrWhiteSpace())
            {
                databaseName = ConnectionStringNameAttribute.GetConnStringName<TMongoDbContext>();
            }

            //TODO: Create only single MongoDbClient per connection string in an application (extract MongoClientCache for example).
            var databaseApi = unitOfWork.FindDatabaseApi(dbContextKey);
            if (databaseApi == null)
            {
                databaseApi = new MongoDbDatabaseApi<TMongoDbContext>(
                    await CreateDbContextAsync(
                        unitOfWork,
                        mongoUrl,
                        databaseName,
                        cancellationToken
                    )
                );

                unitOfWork.AddDatabaseApi(dbContextKey, databaseApi);
            }

            return ((MongoDbDatabaseApi<TMongoDbContext>) databaseApi).DbContext;
        }

        [Obsolete("Use CreateDbContextAsync")]

        private TMongoDbContext CreateDbContext(IUnitOfWork unitOfWork, MongoUrl mongoUrl, string databaseName)
        {
            var client = new MongoClient(mongoUrl);
            var database = client.GetDatabase(databaseName);

            if (unitOfWork.Options.IsTransactional)
            {
                return CreateDbContextWithTransaction(unitOfWork, mongoUrl, client, database);
            }

            var dbContext = unitOfWork.ServiceProvider.GetRequiredService<TMongoDbContext>();
            dbContext.ToAbpMongoDbContext().InitializeDatabase(database, client, null);

            return dbContext;
        }

        private async Task<TMongoDbContext> CreateDbContextAsync(
            IUnitOfWork unitOfWork,
            MongoUrl mongoUrl,
            string databaseName,
            CancellationToken cancellationToken = default)
        {
            var client = new MongoClient(mongoUrl);
            var database = client.GetDatabase(databaseName);

            if (unitOfWork.Options.IsTransactional)
            {
                return await CreateDbContextWithTransactionAsync(
                    unitOfWork,
                    mongoUrl,
                    client,
                    database,
                    cancellationToken
                );
            }

            var dbContext = unitOfWork.ServiceProvider.GetRequiredService<TMongoDbContext>();
            dbContext.ToAbpMongoDbContext().InitializeDatabase(database, client, null);

            return dbContext;
        }

        [Obsolete("Use CreateDbContextWithTransactionAsync")]
        private TMongoDbContext CreateDbContextWithTransaction(
            IUnitOfWork unitOfWork,
            MongoUrl url,
            MongoClient client,
            IMongoDatabase database)
        {
            var transactionApiKey = $"MongoDb_{url}";
            var activeTransaction = unitOfWork.FindTransactionApi(transactionApiKey) as MongoDbTransactionApi;
            var dbContext = unitOfWork.ServiceProvider.GetRequiredService<TMongoDbContext>();

            if (activeTransaction?.SessionHandle == null)
            {
                var session = client.StartSession();

                if (unitOfWork.Options.Timeout.HasValue)
                {
                    session.AdvanceOperationTime(new BsonTimestamp(unitOfWork.Options.Timeout.Value));
                }

                session.StartTransaction();

                unitOfWork.AddTransactionApi(
                    transactionApiKey,
                    new MongoDbTransactionApi(session)
                );

                dbContext.ToAbpMongoDbContext().InitializeDatabase(database, client, session);
            }
            else
            {
                dbContext.ToAbpMongoDbContext().InitializeDatabase(database, client, activeTransaction.SessionHandle);
            }

            return dbContext;
        }

        private async Task<TMongoDbContext> CreateDbContextWithTransactionAsync(
            IUnitOfWork unitOfWork,
            MongoUrl url,
            MongoClient client,
            IMongoDatabase database,
            CancellationToken cancellationToken = default)
        {
            var transactionApiKey = $"MongoDb_{url}";
            var activeTransaction = unitOfWork.FindTransactionApi(transactionApiKey) as MongoDbTransactionApi;
            var dbContext = unitOfWork.ServiceProvider.GetRequiredService<TMongoDbContext>();

            if (activeTransaction?.SessionHandle == null)
            {
                var session = await client.StartSessionAsync(cancellationToken: GetCancellationToken(cancellationToken));

                if (unitOfWork.Options.Timeout.HasValue)
                {
                    session.AdvanceOperationTime(new BsonTimestamp(unitOfWork.Options.Timeout.Value));
                }

                session.StartTransaction();

                unitOfWork.AddTransactionApi(
                    transactionApiKey,
                    new MongoDbTransactionApi(session)
                );

                dbContext.ToAbpMongoDbContext().InitializeDatabase(database, client, session);
            }
            else
            {
                dbContext.ToAbpMongoDbContext().InitializeDatabase(database, client, activeTransaction.SessionHandle);
            }

            return dbContext;
        }

        protected virtual CancellationToken GetCancellationToken(CancellationToken preferredValue = default)
        {
            return _cancellationTokenProvider.FallbackToProvider(preferredValue);
        }
    }
}
