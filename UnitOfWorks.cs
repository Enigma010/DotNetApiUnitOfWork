using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using Logging;

namespace UnitOfWork
{
    /// <summary>
    /// Registers services that need to define a unit of work block that needs to either
    /// all succeed or be rolled back.  The standard usage is the following
    /// <code>
    ///     using (var unitOfWorks = new UnitOfWorks(new IUnitOfWork[] { _repository, _eventPublisher}))
    ///     {
    ///         return await unitOfWorks.RunAsync(async () =>
    ///         {
    ///             // Do application logic here
    ///         });
    ///     }
    /// </code>
    /// Note that the example above uses RunAsync but you could also be using Run.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Core infrastructure, unit tests would at a lower level")]
    public class UnitOfWorks : IDisposable
    {
        private readonly ILogger _logger;
        /// <summary>
        /// The registered services that define unit of work blocks
        /// </summary>
        private List<IUnitOfWork> _unitOfWorks = new List<IUnitOfWork>();
        public UnitOfWorks(IEnumerable<object> possibleUnitOfWorks, ILogger logger)
        {
            _logger = logger;
            using (_logger.LogCaller())
            {
                possibleUnitOfWorks.ToList().ForEach(possibleUnitOfWorks =>
                {
                    if (possibleUnitOfWorks is IUnitOfWork unitOfWork)
                    {
                        string unitOfWorkType = unitOfWork.GetType().Name;
                        _logger.LogInformation("Adding unit of work {UnitOfWorkType}", unitOfWorkType);
                        _unitOfWorks.Add(unitOfWork);
                        _logger.LogInformation("Beginning unit of work {UnitOfWorkType}", unitOfWorkType);
                        unitOfWork.Begin();
                        _logger.LogInformation("Began unit of work {UnitOfWorkType}", unitOfWorkType);
                    }
                });
            }
        }
        /// <summary>
        /// Runs an action, representing the application logic, if successful commits
        /// the changes, otherwise it rolls them back
        /// </summary>
        /// <param name="action">The application logic</param>
        /// <returns></returns>
        public async Task Run(Action action)
        {
            using (_logger.LogCaller())
            {
                using (TransactionScope scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        _logger.LogInformation("Running action");
                        action();
                        _logger.LogInformation("Ran action");
                        await Commit();
                        _logger.LogInformation("Committed");
                        scope.Complete();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception encountered");
                        _logger.LogInformation("Rolling back");
                        await Rollback();
                        _logger.LogInformation("Rolled back");
                        throw;
                    }
                }
            }
        }
        /// <summary>
        /// Runs an async function.  If successful commits the changes, otherwise rolls it back,
        /// and returns the value
        /// </summary>
        /// <typeparam name="RunReturnType">The object type to return</typeparam>
        /// <param name="func">The application function</param>
        /// <returns>The application return object</returns>
        public async Task<RunReturnType> RunAsync<RunReturnType>(Func<Task<RunReturnType>> func)
        {
            using (_logger.LogCaller())
            {
                using (TransactionScope scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        _logger.LogInformation("Running function");
                        var returnValue = await func();
                        _logger.LogInformation("Ran function");
                        await Commit();
                        _logger.LogInformation("Committed");
                        scope.Complete();
                        return returnValue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception encountered");
                        await Rollback();
                        throw;
                    }
                }
            }
        }
        /// <summary>
        /// Runs an async function.  If successful commits the changes, otherwise rolls it back.
        /// </summary>
        /// <param name="func">The application function</param>
        /// <returns></returns>
        public async Task RunAsync(Func<Task> func)
        {
            using (_logger.LogCaller())
            {
                using (TransactionScope scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        _logger.LogInformation("Running function");
                        await func();
                        _logger.LogInformation("Ran function");
                        await Commit();
                        _logger.LogInformation("Committed");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception encountered");
                        await Rollback();
                        throw;
                    }
                }
            }
        }
        /// <summary>
        /// Commits all unit of works
        /// </summary>
        /// <returns></returns>
        public async Task Commit()
        {
            using (_logger.LogCaller())
            {
                foreach (var unitOfWork in _unitOfWorks)
                {
                    _logger.LogInformation("Committing {UnitOfWorkType}", unitOfWork.GetType().Name);
                    await unitOfWork.Commit();
                    _logger.LogInformation("Committed {UnitOfWorkType}", unitOfWork.GetType().Name);
                }
            }
        }
        /// <summary>
        /// Rolls back all unit of works
        /// </summary>
        /// <returns></returns>
        private async Task Rollback()
        {
            using (_logger.LogCaller())
            {
                foreach (var unitOfWork in _unitOfWorks)
                {
                    _logger.LogInformation("Rolling back {UnitOfWorkType}", unitOfWork.GetType().Name);
                    await unitOfWork.Rollback();
                    _logger.LogInformation("Rolled back {UnitOfWorkType}", unitOfWork.GetType().Name);
                }
            }
        }
        /// <summary>
        /// Called during dispose
        /// </summary>
        public async void Dispose()
        {
            await Rollback();
        }
    }
}
