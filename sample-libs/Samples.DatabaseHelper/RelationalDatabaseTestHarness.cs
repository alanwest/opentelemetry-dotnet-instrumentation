using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

// ReSharper disable MethodHasAsyncOverloadWithCancellation
// ReSharper disable MethodSupportsCancellation

namespace Samples.DatabaseHelper
{
    public static class RelationalDatabaseTestHarness
    {
        /// <summary>
        /// Helper method that runs ADO.NET test suite for the specified <see cref="IDbCommandExecutor"/>
        /// in addition to other built-in implementations.
        /// </summary>
        /// <param name="connection">The <see cref="IDbConnection"/> to use to connect to the database.</param>
        /// <param name="commandFactory">A <see cref="DbCommandFactory"/> implementation specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand.</param>
        /// <param name="providerSpecificCommandExecutor">A <see cref="IDbCommandExecutor"/> specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand, used to call DbCommand methods.</param>
        /// <param name="cancellationToken">A cancellation token passed into downstream async methods.</param>
        /// <typeparam name="TCommand">The DbCommand implementation specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand.</typeparam>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync<TCommand>(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IDbCommandExecutor providerSpecificCommandExecutor,
            CancellationToken cancellationToken)
            where TCommand : IDbCommand
        {
            var executors = new List<IDbCommandExecutor>
                            {
                                // call methods directly like SqlCommand.ExecuteScalar(), provided by caller
                                providerSpecificCommandExecutor,

                                // call methods through DbCommand reference
                                new DbCommandClassExecutor(),

                                // call methods through IDbCommand reference
                                new DbCommandInterfaceExecutor(),

                                // call methods through IDbCommand reference, but using a generic constraint
                                new DbCommandInterfaceGenericExecutor<TCommand>(),
#if !NET45
                                // call methods through DbCommand reference (referencing netstandard.dll)
                                new DbCommandNetStandardClassExecutor(),

                                // call methods through IDbCommand reference (referencing netstandard.dll)
                                new DbCommandNetStandardInterfaceExecutor(),

                                // call methods through IDbCommand reference (referencing netstandard.dll), but using a generic constraint
                                new DbCommandNetStandardInterfaceGenericExecutor<TCommand>(),
#endif
                            };

            using (var root = Tracer.Instance.StartActive("root"))
            {
                foreach (var executor in executors)
                {
                    await RunAsync(connection, commandFactory, executor, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Runs ADO.NET test suite for the specified <see cref="IDbCommandExecutor"/>.
        /// </summary>
        /// <param name="connection">The <see cref="IDbConnection"/> to use to connect to the database.</param>
        /// <param name="commandFactory">A <see cref="DbCommandFactory"/> implementation specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand.</param>
        /// <param name="commandExecutor">A <see cref="IDbCommandExecutor"/> used to call DbCommand methods.</param>
        /// <param name="cancellationToken">A cancellation token passed into downstream async methods.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task RunAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IDbCommandExecutor commandExecutor,
            CancellationToken cancellationToken)
        {
            string commandName = commandExecutor.CommandTypeName;
            Console.WriteLine(commandName);

            using (var parentScope = Tracer.Instance.StartActive("command"))
            {
                parentScope.Span.ResourceName = commandName;
                IDbCommand command;

                using (var scope = Tracer.Instance.StartActive("sync"))
                {
                    scope.Span.ResourceName = commandName;

                    Console.WriteLine("  Synchronous");
                    Console.WriteLine();
                    await Task.Delay(100, cancellationToken);

                    command = commandFactory.GetCreateTableCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetInsertRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetSelectScalarCommand(connection);
                    commandExecutor.ExecuteScalar(command);

                    command = commandFactory.GetUpdateRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetSelectRowCommand(connection);
                    commandExecutor.ExecuteReader(command);

                    command = commandFactory.GetSelectRowCommand(connection);
                    commandExecutor.ExecuteReader(command, CommandBehavior.Default);

                    command = commandFactory.GetDeleteRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);
                }

                if (commandExecutor.SupportsAsyncMethods)
                {
                    await Task.Delay(100, cancellationToken);

                    using (var scope = Tracer.Instance.StartActive("async"))
                    {
                        scope.Span.ResourceName = commandName;

                        Console.WriteLine("  Asynchronous");
                        Console.WriteLine();
                        await Task.Delay(100, cancellationToken);

                        command = commandFactory.GetCreateTableCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command);

                        command = commandFactory.GetInsertRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command);

                        command = commandFactory.GetSelectScalarCommand(connection);
                        await commandExecutor.ExecuteScalarAsync(command);

                        command = commandFactory.GetUpdateRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command);

                        command = commandFactory.GetSelectRowCommand(connection);
                        await commandExecutor.ExecuteReaderAsync(command);

                        command = commandFactory.GetSelectRowCommand(connection);
                        await commandExecutor.ExecuteReaderAsync(command, CommandBehavior.Default);

                        command = commandFactory.GetDeleteRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command);
                    }

                    await Task.Delay(100, cancellationToken);

                    using (var scope = Tracer.Instance.StartActive("async-with-cancellation"))
                    {
                        scope.Span.ResourceName = commandName;

                        Console.WriteLine("  Asynchronous with cancellation");
                        Console.WriteLine();
                        await Task.Delay(100, cancellationToken);

                        command = commandFactory.GetCreateTableCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                        command = commandFactory.GetInsertRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                        command = commandFactory.GetSelectScalarCommand(connection);
                        await commandExecutor.ExecuteScalarAsync(command, cancellationToken);

                        command = commandFactory.GetUpdateRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                        command = commandFactory.GetSelectRowCommand(connection);
                        await commandExecutor.ExecuteReaderAsync(command, cancellationToken);

                        command = commandFactory.GetSelectRowCommand(connection);
                        await commandExecutor.ExecuteReaderAsync(command, CommandBehavior.Default, cancellationToken);

                        command = commandFactory.GetDeleteRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);
                    }
                }
            }

            await Task.Delay(100, cancellationToken);
        }
    }
}
